using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

using QuantConnect.Indicators;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using QuantConnect.Brokerages;

//Mebane Faber Relative Strength Strategy with MA Rule
//This is a synthesis of two methods

//Relative Strength Strategies for Investing
// Asset Class Momentum - Rotational System
//http://papers.ssrn.com/sol3/papers.cfm?abstract_id=1585517

//A Quantitative Approach to Tactical Asset Allocation
// Asset Class Trend Following
// Mebane Faber's MA Rule
//http://papers.ssrn.com/sol3/papers.cfm?abstract_id=962461

//I think it's pretty common and Mebane probably has published this somewhere.
//1.Measure the M-month trailing returns of a basket of stocks

//2.Rank the stocks and buy the top-K if monthly price > 10-month SMA.

//3.Else, hold cash

//It reduces the drawdown of the relative strength approach.

namespace QuantConnect.Algorithm.CSharp
{
    class Mebane : QCAlgorithm
    {
        //const values
        private const decimal TOTALCASH = 10000;                //total capital
        private const decimal LEVERAGE = 1.0M;
        private const int TOP_K = 3;                            //TOP_K > 0
        private const int HS = 280;                             //history span
        private const int WD1 = 61;                              //days of window1
        private const int WD2 = 25;                              //days of window2
        //private const int HS = 2;                             //history span
        //private const int WD1 = 1;                              //days of window1
        //private const int WD2 = 1;                              //days of window2
        private const decimal MIN_PCT_DIFF = 0.1M;

        private readonly Dictionary<Symbol, SymbolData> _sd = new Dictionary<Symbol, SymbolData>();      //portfolio corresponding dic

        public override void Initialize()
        {
            //set trade period
            SetStartDate(2010, 6, 1);  //Set Start Date
            SetEndDate(2018, 6, 1);    //Set End Date

            //set total capital
            SetCash(TOTALCASH);             //Set Strategy Cash

            SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);

            //select stocks to be traded.
            stockSelection();

            DateTimeZone TimeZone = DateTimeZoneProviders.Tzdb["America/New_York"];

            Schedule.On(DateRules.EveryDay(), TimeRules.At(9, 40, TimeZone), () =>
            {
                List<SymbolData> ranks = new List<SymbolData>();
                decimal tmp = 0;

                foreach (var val in _sd.Values)
                {
                    if (!val.IsReady || !val.Security.Exchange.DateIsOpen(Time))
                        continue;

                    Transactions.CancelOpenOrders(val.Symbol);      //close all open orders at the daily beginning

                    var tradeBarHistory = History<TradeBar>(val.Symbol, TimeSpan.FromDays(HS), Resolution.Daily);

                    //Calculate LSma
                    foreach (TradeBar tradeBar in tradeBarHistory)
                    {
                        tmp = tmp + tradeBar.Close;

                        //val.LSma.Update(tradeBar.EndTime, tradeBar.Close);
                        //val.SSma.Update(tradeBar.EndTime, tradeBar.Close);
                    }
                    if (tradeBarHistory.Count() > 0)
                        val.LSma = tmp / tradeBarHistory.Count();
                    else
                        val.LSma = 0;

                    //Calculate SSma
                    int i = 0;
                    int count;
                    tmp = 0;
                    if (tradeBarHistory.Count() - WD2 > 0)
                    {
                        i = tradeBarHistory.Count() - WD2;
                        count = WD2;
                    }
                    else
                        count = tradeBarHistory.Count();
                    for (int j = i; j < tradeBarHistory.Count(); j++)
                        tmp = tmp + tradeBarHistory.ElementAt(j).Close;
                    if (count > 0)
                        val.SSma = tmp / count;
                    else
                        val.SSma = 0;

                    //System.Console.WriteLine("Count: " + tradeBarHistory.Count()); 
                    if (tradeBarHistory.Count() > 0)
                        if (tradeBarHistory.Count() - 1 - WD1 >= 0)
                            tmp = tradeBarHistory.ElementAt(tradeBarHistory.Count() - 1).Close
                                - tradeBarHistory.ElementAt(tradeBarHistory.Count() - 1 - WD1).Close;
                        else
                            tmp = tradeBarHistory.ElementAt(tradeBarHistory.Count() - 1).Close
                                - tradeBarHistory.ElementAt(0).Close;
                    else
                        tmp = 0;
                    val.Return = tmp;
                    ranks.Add(val);
                }

                if (ranks.Count < 1)
                    return;

                ranks.Sort(delegate (SymbolData x, SymbolData y) { return y.CompareTo(x); });

                for (int i = 0; i < ranks.Count; i++)
                {
                    if (i < TOP_K && ranks.ElementAt(i).SSma - ranks.ElementAt(i).LSma > 0)
                    {
                        ranks.ElementAt(i).wt = LEVERAGE / TOP_K;
                    }
                    else
                        ranks.ElementAt(i).wt = 0;
                }

                reweight();
            });
        }

        private void stockSelection()
        {
            _sd.Clear();

            //Add individual stocks.
            AddEquity("AAPL", Resolution.Minute, Market.USA);
            AddEquity("MSFT", Resolution.Minute, Market.USA);
            AddEquity("INTC", Resolution.Minute, Market.USA);
            AddEquity("AMZN", Resolution.Minute, Market.USA);
            AddEquity("GOOGL", Resolution.Minute, Market.USA);
            AddEquity("FB", Resolution.Minute, Market.USA);
            AddEquity("BABA", Resolution.Minute, Market.USA);

            foreach (var security in Securities)
            {
                _sd.Add(security.Key, new SymbolData(security.Key, this));
            }
        }

        private void reweight()
        {
            decimal liquidity = Portfolio.TotalHoldingsValue + Portfolio.Cash;

            if (liquidity <= 0)
                return;

            decimal pct_diff = 0;

            foreach (var val in _sd.Values)
            {
                decimal target;
                if (val.Security.High > 0)
                    target = liquidity * val.wt / val.Security.High;
                else
                    target = 0;
                decimal current = Portfolio[val.Symbol].Quantity;
                val.orders = target - current;
                pct_diff += Math.Abs(val.orders * val.Security.High / liquidity);
            }

            if (pct_diff > MIN_PCT_DIFF)
            {
                foreach (var val in _sd.Values)
                {
                    MarketOrder(val.Symbol, val.orders);
                }
            }
        }

        class SymbolData : IComparable
        {
            public readonly Symbol Symbol;
            public readonly Security Security;

            public decimal Quantity
            {
                get { return Security.Holdings.Quantity; }
            }

            public decimal LSma;
            public decimal SSma;
            public readonly Identity Close;
            public decimal Return;
            public decimal wt;
            public decimal orders;

            private readonly Mebane _algorithm;

            public SymbolData(Symbol symbol, Mebane algorithm)
            {
                Symbol = symbol;
                Security = algorithm.Securities[symbol];

                Close = algorithm.Identity(symbol);

                _algorithm = algorithm;
            }

            public bool IsReady
            {
                get { return Close.IsReady; }
            }

            public int CompareTo(object obj)
            {
                if (obj == null) return 1;

                SymbolData other = obj as SymbolData;
                if (other != null)
                    return this.Return.CompareTo(other.Return);
                else
                    throw new ArgumentException("Object is not a SymbolData");
            }
        }
    }
}