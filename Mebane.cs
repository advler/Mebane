using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Securities.Equity;
using QuantConnect.Indicators;
using QuantConnect.Securities;

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

            Schedule.On(DateRules.EveryDay(), TimeRules.At(9, 32, ), () =>
            foreach (var val in _sd.Values)
            {
                Schedule.On(DateRules.EveryDay(val.Symbol), TimeRules.AfterMarketOpen(val.Symbol), () =>
                {
                    Debug("EveryDay." + val.Symbol.ToString() + " initialize at: " + Time);
                    Transactions.CancelOpenOrders(val.Symbol);                  //close all open orders at the daily beginning
                });
            }


        }

        private void stockSelection()
        {
            _sd.Clear();

            //Add individual stocks.
            AddEquity("AAPL", Resolution.Daily, Market.USA);
            AddEquity("IBM", Resolution.Daily, Market.USA);
            AddEquity("INTC", Resolution.Daily, Market.USA);

            foreach (var security in Securities)
            {
                _sd.Add(security.Key, new SymbolData(security.Key, this));
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

            public readonly SimpleMovingAverage LSma;
            public readonly SimpleMovingAverage SSma;
            public readonly Identity Close;

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

            public void Update()
            {
                //reset LastFillPrice
                if ((int)(Security.Holdings.Quantity) == 0)
                    LastFillPrice = -1;

                OrderTicket ticket;                             //enter ticket
                List<int> idlist;                                //force-quit id list

                TryForceQuit(out idlist);                   //止损
                TryExit(out idlist);                        //退出
                TryEnter(out ticket);                       //入市
            }

        }
    }

   
}
