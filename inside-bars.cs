using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


/*

    Features:
        (Strategy)
        - Set the amount of Inside bars for the signal between 1 - 3.
        - Set the max candle size for it to be a valid signal.
        - Velja Max spread.
        (Filters)
        - Use Ema of your choice to Go long above and short under
        (Risk)
        - Choose tradesize
        - Stoploss
        - Or use candlesize as stop loss
        - And use RU TP if Candle stop loss is used
        - TakeProfit
        - Control if only 1 trade can be opened at a time.
        - Set to breakeven at 1 ru
        
    What is left to do?
        - Test it on stocks to see if SL/TP and share size is correct.


*/
namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class InsidebarStrategy : Robot
    {   
        #region Prameters
        
        [Parameter("Inside bar count", DefaultValue = 1, MinValue = 1, MaxValue = 3, Step = 1, Group = "Strategy settings")]
        public int InsideBarCount { get; set; }
        
        [Parameter("Max candle size TICKS", DefaultValue = 50, MinValue = 1, MaxValue = 500, Step = 1, Group = "Strategy settings")]
        public int MaxInsideBarCandleSize { get; set; }
        
        [Parameter("Max spread", DefaultValue = 1, MinValue = 0.1, MaxValue = 100, Step = 0.1, Group = "Strategy settings")]
        public int MaxSpread { get; set; }
        
        [Parameter("Use EMA, Long Above, Short Under", DefaultValue = true, Group = "EMA settings")]
        public bool UseEma { get; set; }
        
        [Parameter("Source", DefaultValue = 50, MinValue = 1, MaxValue = 500, Step = 1, Group = "EMA settings")]
        public DataSeries EmaSrc { get; set; }
        
        [Parameter("Periods", DefaultValue = 50, MinValue = 1, MaxValue = 500, Step = 1, Group = "EMA settings")]
        public int EmaPeriods { get; set; }
        
        [Parameter("Use candle size as Stoploss", DefaultValue = true, Group = "Risk settings")]
        public bool UseCandleStopLoss { get; set; }
        
        [Parameter("Allow more than 1 open trade", DefaultValue = true, Group = "Risk settings")]
        public bool AllowOpenMoreTrades { get; set; }
        
        [Parameter("Move to breakeven on 1:1", DefaultValue = true, Group = "Risk settings")]
        public bool MoveToBreakEvenVal { get; set; }
        
        [Parameter("Risk Unit Takeprofit (If use candle size = Yes)", DefaultValue = 2, MinValue = 0.5, MaxValue = 100000, Step = 0.01, Group = "Candlesize Risk settings")]
        public double TakeProfitRu { get; set; }
        
        [Parameter("Risk Unit in $, (Lot size is automatically calculated)", DefaultValue = 2, MinValue = 1, MaxValue = 100000, Step = 1, Group = "Candlesize Risk settings")]
        public double RiskUnit { get; set; }
        
        [Parameter("Add % to stoploss (For slippage)", DefaultValue = 10.0, MinValue = 0, MaxValue = 100, Step = 0.1, Group = "Candlesize Risk settings")]
        public double AddToSl { get; set; }
        
        [Parameter("Lots(forex) or Shares(stocks)", DefaultValue = 1, MinValue = 0.01, MaxValue = 100000, Step = 0.01, Group = "Manual Risk settings")]
        public double TradeSize { get; set; }
        
        [Parameter("Stoploss (If Use candle size as Stoploss = No)", DefaultValue = 20, MinValue = 0, MaxValue = 100000, Step = 1, Group = "Manual Risk settings")]
        public double StopLoss { get; set; }
        
        [Parameter("Takeprofit (If Use candle size as Stoploss = No)", DefaultValue = 40, MinValue = 0, MaxValue = 100000, Step = 1, Group = "Manual Risk settings")]
        public double TakeProfit { get; set; }
        
        #endregion
        
        
        

        #region Variables
        private double BreakHighOf = 0;
        private double BreakLowOf = 0;
        private bool CanTrade = false;
        private double CandleSize = 0;
        
        private double EMA;
        #endregion


        #region cBot Events
        protected override void OnStart()
        {
            Positions.Opened += TradeOpened;
        }

        protected override void OnBar()
        {
            EMA = Indicators.ExponentialMovingAverage(EmaSrc, EmaPeriods).Result.LastValue;
            Inside();
        }
        
        
        protected override void OnTick()
        {
            CheckBreakOut();
            
            
            if(MoveToBreakEvenVal)
            {
                MoveToBreakEven();
            }
        }


        protected override void OnStop()
        {
            // Handle cBot stop here
            Print("Inside Bar AE has stopped");
        }
        #endregion
        
        
        #region Helpers
        private int RandomNum()
        {
            Random r = new Random();
            return r.Next(0, 1000000);
        }
        
        
        private void ColorBar(int count, int index)
        {
            for(int i = count; i > 0; i--)
            {
               if(i == count)
               {
                    Chart.SetBarFillColor(index - i, Color.DeepPink);
                    Chart.SetBarOutlineColor(index - i, Color.DeepPink);
               }
               else
               {
                    Chart.SetBarFillColor(index - i, Color.Orange);
                    Chart.SetBarOutlineColor(index - i, Color.Orange);
               }
            }
        }
        
        
        private bool CheckCandleSize(int index)
        {
            double open = Bars.HighPrices[index];
            double close = Bars.LowPrices[index];
            double candleSize = open - close;
          
            double max = MaxInsideBarCandleSize * Symbol.TickSize;

            if(max >= candleSize)
            {
                CandleSize = candleSize;
                return true;
            }
            return false;
        }
        #endregion


        #region Indicators Check
        private string CheckEma()
        {
            if(UseEma)
            {
                double open = Bars.OpenPrices[Bars.Count - 1];
                double close = Bars.ClosePrices[Bars.Count - 1];
                double prize = open > close ? open : close; 
                if(prize > EMA)
                {
                    return "L";
                }
                else if (prize < EMA)
                {
                    return "S";
                }
                return "N";
            }
            return "B";
        }
        #endregion


        #region Strategy

        private void Inside()
        {
            // Use the brute Force function to find the inside bars bc for some reason the variable one showes false signals.
            int index = Bars.Count - 1;

            
            if(
                InsideBarCount == 1 
                && Bars.HighPrices[index - 2] > Bars.HighPrices[index - 1] 
                && Bars.LowPrices[index - 2] < Bars.LowPrices[index - 1]
                && CheckCandleSize(index - 2)
                )
            {
                ColorBar(2, index);
                BreakHighOf = Bars.HighPrices[index - 1];
                BreakLowOf = Bars.LowPrices[index - 1];
                CanTrade = true;
            }
            else if(
                    InsideBarCount == 2 
                    && Bars.HighPrices[index - 3] > Bars.HighPrices[index - 2] 
                    && Bars.LowPrices[index - 3] < Bars.LowPrices[index - 2]
                    && Bars.HighPrices[index - 3] > Bars.HighPrices[index - 1] 
                    && Bars.LowPrices[index - 3] < Bars.LowPrices[index - 1]
                    && CheckCandleSize(index - 3)
                    )
            {
                ColorBar(3, index);
                BreakHighOf = Bars.HighPrices[index - 1];
                BreakLowOf = Bars.LowPrices[index - 1];
                //PlaceStopLimitOrder(TradeType.Buy, Symbol.Name, 100000, BreakHighOf, 1, "long", 5, 10);
                //PlaceStopLimitOrder(TradeType.Sell, Symbol.Name, 100000, BreakHighOf, 1, "long", 5, 10);
                CanTrade = true;
            }
            else if(
                    InsideBarCount == 3
                    && Bars.HighPrices[index - 4] > Bars.HighPrices[index - 3] 
                    && Bars.LowPrices[index - 4] < Bars.LowPrices[index - 3]
                    && Bars.HighPrices[index - 4] > Bars.HighPrices[index - 2] 
                    && Bars.LowPrices[index - 4] < Bars.LowPrices[index - 2]
                    && Bars.HighPrices[index - 4] > Bars.HighPrices[index - 1] 
                    && Bars.LowPrices[index - 4] < Bars.LowPrices[index - 1]
                    && CheckCandleSize(index - 4)
                    )
            {
                ColorBar(4, index);
                BreakHighOf = Bars.HighPrices[index - 1];
                BreakLowOf = Bars.LowPrices[index - 1];
                CanTrade = true;
            }
        }
        
        
        private void TradeOpened(PositionOpenedEventArgs args)
        {
            if(PendingOrders.Count == 0)
            {
                return;
            }
            foreach(PendingOrder order in PendingOrders)
            {
                order.Cancel();
            }
        }
        
        
        private void CheckBreakOut()
        {
            if(!CanTrade)
                return;
                
            string d = CheckEma();
            
            double close = Bars.ClosePrices[Bars.Count - 1];
            //double low = Bars.LowPrices[Bars.Count - 1];

            if(Functions.HasCrossedAbove(Bars.ClosePrices, BreakHighOf, 0) & d == "L" | Functions.HasCrossedAbove(Bars.ClosePrices, BreakHighOf, 1) & d == "B")
            {
                // Execute long trade
                Print("Broke for long");
                ExecuteTrade(TradeType.Buy);
                CanTrade = false;
            }
            else if(Functions.HasCrossedBelow(Bars.ClosePrices, BreakLowOf, 0) & d == "S" | Functions.HasCrossedBelow(Bars.ClosePrices, BreakLowOf, 0) & d == "B")
            {
                // Execute short trade
                Print("Broke for short");
                ExecuteTrade(TradeType.Sell);
                CanTrade = false;
            }
        }

        #endregion


        #region Trade exicution
        
        private void ExecuteTrade(TradeType TrType)
        {
            if(AllowMoreThanOneTradeCheck() || Symbol.Spread > MaxSpread)
                return;
              
            double volume = DetermineTradeSize();
            double sl = CalcStopLoss();
            double tp = CalcTakeProfit();
            
            var res = ExecuteMarketOrder(TrType, Symbol.Name, volume, string.Format("{0}", RandomNum()), sl , tp);
            if(res.IsSuccessful)
            {
                Print("Res success Entry price: ", res.Position.EntryPrice);
                Print("Res success SL: ", res.Position.StopLoss);
                Print("Res success TP: ", res.Position.TakeProfit);
            }
            else
            {
                Print("Error: ", res.Error);
            }
        }

        #endregion


        #region BreakEven

        private void MoveToBreakEven()
        {

            if (Positions.Count == 0)
            {
                return;
            }
            var position = Positions.First();
            if(position.Pips >= StopLoss | position.GrossProfit >= RiskUnit)
            {
                double add = position.TradeType == TradeType.Buy ? Symbol.TickSize * 5 : -Symbol.TickSize * 5;
                ModifyPosition(position, position.EntryPrice + add, position.TakeProfit);
            }  
        }
        
        #endregion


        #region Sl TP Risk Sharesize
        private double DetermineTradeSize()
        {
            if(Symbol.TickSize == 0.01)
            {
                // Stocks so its share size
                return UseCandleStopLoss ? Math.Round(RiskUnit / Math.Round(CandleSize, Symbol.Digits)) : TradeSize;
            }
            else 
            {
                // Forex
                double vol = UseCandleStopLoss ? Symbol.NormalizeVolumeInUnits(Math.Round(RiskUnit / Math.Round(CandleSize, Symbol.Digits))) : Symbol.NormalizeVolumeInUnits(TradeSize * 100000);
                return vol;
            }
        }
        
        private bool AllowMoreThanOneTradeCheck()
        {
            var pos = Positions.Count;
            if(!AllowOpenMoreTrades & pos > 0)
                return true;
                
            return false;
        }
        
        
        private double CalcStopLoss()
        {
            if(Symbol.TickSize == 0.01)
            {
                var spread = (Symbol.Spread);
                return UseCandleStopLoss ? Math.Round(CandleSize * (1 + (AddToSl / 100)), Symbol.Digits) + spread: (StopLoss * Symbol.TickSize) + spread;
            }
            else 
            {
                // Forex
                return UseCandleStopLoss ? Math.Round(CandleSize * (1 + (AddToSl / 100)), Symbol.Digits) * 10000 : StopLoss;
            }
        }
        
        private double CalcTakeProfit()
        {
            if(Symbol.TickSize == 0.01)
            {
                // Stocks so its share size
                return UseCandleStopLoss ? Math.Round(CandleSize, Symbol.Digits) * TakeProfitRu : TakeProfit * Symbol.TickSize;
            }
            else 
            {
                // Forex
                return UseCandleStopLoss ? (Math.Round(CandleSize, Symbol.Digits) * 10000) * TakeProfitRu : TakeProfit;
            }
        }

        #endregion
    }
}