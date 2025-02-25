using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FibonacciTradingBot : Robot
    {
        [Parameter("Lot Size", DefaultValue = 0.01, MinValue = 0.01, MaxValue = 0.1)]
        public double LotSize { get; set; }

        [Parameter("Stop Loss (Pips)", DefaultValue = 50, MinValue = 1)]
        public double StopLossPips { get; set; }

        [Parameter("Take Profit (Pips)", DefaultValue = 100, MinValue = 1)]
        public double TakeProfitPips { get; set; }

        [Parameter("Fibonacci Period", DefaultValue = 20, MinValue = 10)]
        public int FibPeriod { get; set; }

        [Parameter("Risk Percentage", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 2.0)]
        public double RiskPercentage { get; set; }

        private MovingAverage ma;
        private double[] fibLevels = { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 };
        private const int MIN_CANDLES_FOR_SWING = 5;

        protected override void OnStart()
        {
            ma = Indicators.MovingAverage(Bars.ClosePrices, FibPeriod, MovingAverageType.Exponential);
            ValidateParameters();
        }

        private void ValidateParameters()
        {
            var minVolume = Symbol.VolumeInUnitsMin;
            var maxVolume = Symbol.VolumeInUnitsMax;
            var volumeStep = Symbol.VolumeInUnitsStep;

            // Adjust lot size to match symbol requirements
            LotSize = Math.Max(minVolume, Math.Min(maxVolume, 
                Math.Round(LotSize / volumeStep) * volumeStep));

            Print($"Adjusted Lot Size: {LotSize}");
            Print($"Min Volume: {minVolume}, Max Volume: {maxVolume}, Step: {volumeStep}");
        }

                protected override void OnTick()
        {
            if (!HasPosition())
            {
                AnalyzeMarket();
            }
        }

private void AnalyzeMarket()
        {
            var trend = IdentifyTrend();
            if (trend == 0) return;

            var swingHigh = FindSwingHigh();
            var swingLow = FindSwingLow();
            if (swingHigh == 0 || swingLow == 0) return;

            var fibRetracementLevels = CalculateFibLevels(swingLow, swingHigh);
            var currentPrice = Symbol.Bid;

            if (trend > 0 && IsValidBuySetup(currentPrice, fibRetracementLevels))
            {
                ExecuteBuyTrade();
            }
            else if (trend < 0 && IsValidSellSetup(currentPrice, fibRetracementLevels))
            {
                ExecuteSellTrade();
            }
        }

        private int IdentifyTrend()
        {
            double currentMa = ma.Result.Last(0);
            double prevMa = ma.Result.Last(1);
            
            if (currentMa > prevMa) return 1;
            if (currentMa < prevMa) return -1;
            return 0;
        }

        private double FindSwingHigh()
        {
            double highest = double.MinValue;
            for (int i = 0; i < MIN_CANDLES_FOR_SWING; i++)
            {
                highest = Math.Max(highest, Bars.HighPrices.Last(i));
            }
            return highest;
        }

        private double FindSwingLow()
        {
            double lowest = double.MaxValue;
            for (int i = 0; i < MIN_CANDLES_FOR_SWING; i++)
            {
                lowest = Math.Min(lowest, Bars.LowPrices.Last(i));
            }
            return lowest;
        }

        private double[] CalculateFibLevels(double low, double high)
        {
            double range = high - low;
            double[] levels = new double[fibLevels.Length];
            
            for (int i = 0; i < fibLevels.Length; i++)
            {
                levels[i] = low + (range * fibLevels[i]);
            }
            
            return levels;
        }

        private bool IsValidBuySetup(double price, double[] fibLevels)
        {
            return price >= fibLevels[2] && price <= fibLevels[3]; // Between 38.2% and 50%
        }

        private bool IsValidSellSetup(double price, double[] fibLevels)
        {
            return price >= fibLevels[3] && price <= fibLevels[4]; // Between 50% and 61.8%
        }

        private void ExecuteBuyTrade()
        {
            try
            {
                double stopLoss = Symbol.Bid - (StopLossPips * Symbol.PipSize);
                double takeProfit = Symbol.Bid + (TakeProfitPips * Symbol.PipSize);
                
                var result = ExecuteMarketOrder(TradeType.Buy, Symbol, LotSize, "FibBuy", 
                    StopLossPips, TakeProfitPips);
                
                if (result.IsSuccessful)
                    Print($"Buy order executed successfully at {result.Position.EntryPrice}");
                else
                    Print($"Buy order failed: {result.Error}");
            }
            catch (Exception ex)
            {
                Print($"Error executing buy trade: {ex.Message}");
            }
        }

        private void ExecuteSellTrade()
        {
            try
            {
                double stopLoss = Symbol.Ask + (StopLossPips * Symbol.PipSize);
                double takeProfit = Symbol.Ask - (TakeProfitPips * Symbol.PipSize);
                
                var result = ExecuteMarketOrder(TradeType.Sell, Symbol, LotSize, "FibSell", 
                    StopLossPips, TakeProfitPips);
                
                if (result.IsSuccessful)
                    Print($"Sell order executed successfully at {result.Position.EntryPrice}");
                else
                    Print($"Sell order failed: {result.Error}");
            }
            catch (Exception ex)
            {
                Print($"Error executing sell trade: {ex.Message}");
            }
        }

        private bool HasPosition()
        {
            return Positions.Find("FibBuy") != null || Positions.Find("FibSell") != null;
        }
    }
}