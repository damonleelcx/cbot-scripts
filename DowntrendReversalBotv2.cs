using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class DowntrendReversalBot : Robot
    {
        [Parameter("Take Profit (Pips)", DefaultValue = 50)]
        public double TakeProfitPips { get; set; }

        [Parameter("Stop Loss (Pips)", DefaultValue = 30)]
        public double StopLossPips { get; set; }

        [Parameter("Order Lot Size", DefaultValue = 1.0)]
        public double LotSize { get; set; }

        [Parameter("EMA Fast Period", DefaultValue = 20)]
        public int EmaFastPeriod { get; set; }

        [Parameter("EMA Slow Period", DefaultValue = 50)]
        public int EmaSlowPeriod { get; set; }

        [Parameter("RSI Period", DefaultValue = 14)]
        public int RsiPeriod { get; set; }

        [Parameter("RSI Oversold Level", DefaultValue = 32)]
        public double RsiOversoldLevel { get; set; }

        [Parameter("RSI Look Back Periods", DefaultValue = 5)]
        public int RsiLookBackPeriods { get; set; }

        [Parameter("Minimum RSI Up Ticks", DefaultValue = 2)]
        public int MinRsiUpTicks { get; set; }

        [Parameter("Minimum RSI Angle", DefaultValue = 15.0)]
        public double MinRsiAngle { get; set; }

        private MovingAverage emaFast;
        private MovingAverage emaSlow;
        private RelativeStrengthIndex rsi;
        private DateTime lastTradeTime;
        private const int MinutesBetweenTrades = 5;
        private double previousLow;
        private int consecutiveLowerLows;

        protected override void OnStart()
        {
            emaFast = Indicators.MovingAverage(Bars.ClosePrices, EmaFastPeriod, MovingAverageType.Exponential);
            emaSlow = Indicators.MovingAverage(Bars.ClosePrices, EmaSlowPeriod, MovingAverageType.Exponential);
            rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, RsiPeriod);

            lastTradeTime = DateTime.MinValue;
            previousLow = double.MaxValue;
            consecutiveLowerLows = 0;
            
            Print("Bot started with improved RSI detection");
        }

        protected override void OnTick()
        {
            if (Bars.Count < Math.Max(EmaSlowPeriod, RsiLookBackPeriods))
                return;

            ManagePositions();

            if (!HasPosition() && Time - lastTradeTime >= TimeSpan.FromMinutes(MinutesBetweenTrades))
            {
                CheckForTradeSetup();
            }
        }

        private void ManagePositions()
        {
            foreach (var position in Positions)
            {
                if (position.SymbolName != SymbolName)
                    continue;

                double profitInPips = position.Pips;

                if (profitInPips >= TakeProfitPips)
                {
                    Print($"Take profit target reached. Closing position. Profit in pips: {profitInPips:F2}");
                    ClosePosition(position);
                    lastTradeTime = Time;
                }
            }
        }

        private bool IsRsiTurningUp()
        {
            // Get RSI values for analysis
            double[] rsiValues = new double[RsiLookBackPeriods];
            for (int i = 0; i < RsiLookBackPeriods; i++)
            {
                rsiValues[i] = rsi.Result.Last(i);
            }

            // Check for consecutive up ticks in recent periods
            int upTicks = 0;
            for (int i = 0; i < rsiValues.Length - 1; i++)
            {
                if (rsiValues[i] > rsiValues[i + 1])
                {
                    upTicks++;
                }
                else
                {
                    upTicks = 0;
                }
            }

            // Calculate RSI angle using linear regression
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            for (int i = 0; i < rsiValues.Length; i++)
            {
                sumX += i;
                sumY += rsiValues[i];
                sumXY += i * rsiValues[i];
                sumXX += i * i;
            }

            double n = rsiValues.Length;
            double slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            double angleInDegrees = Math.Atan(slope) * (180 / Math.PI);

            // Log analysis
            Print($"RSI Analysis - Up ticks: {upTicks}, Angle: {angleInDegrees:F2}°");
            Print($"Recent RSI values: {string.Join(", ", rsiValues.Select(v => v.ToString("F2")))}");

            // Combined conditions for RSI reversal
            bool hasEnoughUpTicks = upTicks >= MinRsiUpTicks;
            bool hasStrongAngle = angleInDegrees >= MinRsiAngle;
            bool isNearMin = rsiValues.Min() <= RsiOversoldLevel;

            return hasEnoughUpTicks && hasStrongAngle && isNearMin;
        }

        private void CheckForTradeSetup()
        {
            double currentPrice = Symbol.Bid;

            if (currentPrice < previousLow)
            {
                consecutiveLowerLows++;
                previousLow = currentPrice;
            }
            else if (currentPrice > previousLow + (10 * Symbol.PipSize))
            {
                consecutiveLowerLows = 0;
                previousLow = currentPrice;
            }

            bool rsiTurningUp = IsRsiTurningUp();
            bool enoughDowntrend = consecutiveLowerLows >= 3;

            Print($"Trade conditions - RSI turning up: {rsiTurningUp}, Enough downtrend: {enoughDowntrend}");
            Print($"Consecutive lower lows: {consecutiveLowerLows}");

            if (rsiTurningUp && enoughDowntrend)
            {
                Print("All conditions met - Attempting to place trade");
                PlaceBuyTrade();
            }
        }

        private void PlaceBuyTrade()
        {
            var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, LotSize, "Downtrend Reversal", StopLossPips, TakeProfitPips);
            
            if (result.IsSuccessful)
            {
                Print("Trade executed successfully!");
                Print($"Entry Price: {result.Position.EntryPrice}");
                Print($"Stop Loss: {StopLossPips} pips");
                Print($"Take Profit: {TakeProfitPips} pips");
                lastTradeTime = Time;
                consecutiveLowerLows = 0;
                previousLow = Symbol.Bid;
            }
            else
            {
                Print($"Trade execution failed: {result.Error}");
            }
        }

        private bool HasPosition()
        {
            return Positions.Count(position => position.SymbolName == SymbolName) > 0;
        }
    }
}