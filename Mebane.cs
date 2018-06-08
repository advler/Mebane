using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

using QuantConnect.Securities.Equity;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using QuantConnect.Data;
using QuantConnect.Data.Market;

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
        private const decimal TOTALCASH = 10000;                //总资金
        private const decimal LEVERAGE = 1.0M;
        private const int TOP_K = 3;
        private const int HS = 200;                             //history span
        private const int WD1 = 61;                              //days of window1
        private const int WD2 = 20;                              //days of window2
        private const decimal MIN_PCT_DIFF = 0.1M;

        private readonly Dictionary<Symbol, SymbolData> _sd = new Dictionary<Symbol, SymbolData>();      //portfolio corresponding dic

        public override void Initialize()
        {
            //set trade period
            SetStartDate(2010, 01, 01);  //Set Start Date
            SetEndDate(2018, 05, 01);    //Set End Date

            //设置总资金
            SetCash(TOTALCASH);             //Set Strategy Cash

            //select stocks to be traded.
            stockSelection();

            Schedule.On(DateRules.EveryDay(), TimeRules.At(9, 35), () =>
            {
                List<SymbolData> ranks = new List<SymbolData>();
                int i = 0;
                Boolean ready = true;

                foreach (var val in _sd.Values)
                {
                    var tradeBarHistory = History<TradeBar>(val.Symbol, TimeSpan.FromDays(HS), Resolution.Daily);
                    
                    foreach (TradeBar tradeBar in tradeBarHistory)
                    {
                        val.LSma.Update(tradeBar.EndTime, tradeBar.Close);
                        val.SSma.Update(tradeBar.EndTime, tradeBar.Close);
                        i++;
                    }
                    var tmp = tradeBarHistory.ElementAt(i - 1).Close - tradeBarHistory.ElementAt(i - 1 - WD1).Close;
                    val.Return = tmp;
                    ranks.Add(val);
                    ready = val.IsReady;
                }

                if (!ready)
                    return;
 
                ranks.Sort(delegate (SymbolData x, SymbolData y) { return y.CompareTo(x); });

                for (i = 0; i < ranks.Count; i++)
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
            AddEquity("AAPL", Resolution.Second, Market.USA);
            AddEquity("IBM", Resolution.Second, Market.USA);
            AddEquity("INTC", Resolution.Second, Market.USA);

            foreach (var security in Securities)
            {
                _sd.Add(security.Key, new SymbolData(security.Key, this));
            }
        }

        private void reweight()
        {
            decimal liquidity = Portfolio.TotalHoldingsValue + Portfolio.Cash;
            decimal pct_diff = 0;

            foreach (var val in _sd.Values)
            {
                decimal target = liquidity * val.wt / val.Security.High;
                decimal current = Portfolio[val.Symbol].Quantity;
                val.orders = target - current;
                pct_diff += val.orders * val.Security.High / liquidity;
            }

            if (pct_diff > MIN_PCT_DIFF)
            {
                foreach (var val in _sd.Values)
                {
                    MarketOrder(val.Symbol, val.orders);
                }
            }
        }

        class SymbolData
        {
            public readonly Symbol Symbol;
            public readonly Security Security;

            public decimal Quantity
            {
                get { return Security.Holdings.Quantity; }
            }

            public SimpleMovingAverage LSma;
            public SimpleMovingAverage SSma;
            public readonly Identity Close;
            public decimal Return;
            public decimal wt;
            public decimal orders;

           private readonly Mebane _algorithm;

            public SymbolData(Symbol symbol, Mebane algorithm)
            {
                Symbol = symbol;
                Security = algorithm.Securities[symbol];

                LSma = new SimpleMovingAverage(HS);
                SSma = new SimpleMovingAverage(WD2);

                _algorithm = algorithm;
            }

            public bool IsReady
            {
                get { return Close.IsReady && LSma.IsReady & LSma.IsReady; }
            }
        }
    }

   
}
