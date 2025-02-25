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
            
            Print("Bot started with modified parameters");
            Print($"RSI Oversold Level: {RsiOversoldLevel}");
        }

        protected override void OnTick()
        {
            if (Bars.Count < EmaSlowPeriod)
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
                Print($"Position profit in pips: {profitInPips:F2}, Target: {TakeProfitPips}");

                if (profitInPips >= TakeProfitPips)
                {
                    Print($"Take profit target reached. Closing position. Profit in pips: {profitInPips:F2}");
                    ClosePosition(position);
                    lastTradeTime = Time;
                }
            }
        }

        private void CheckForTradeSetup()
        {
            double currentPrice = Symbol.Bid;
            double currentRsi = rsi.Result.Last(0);
            double previousRsi = rsi.Result.Last(1);
            double twoBarsAgoRsi = rsi.Result.Last(2);

            if (currentPrice < previousLow)
            {
                consecutiveLowerLows++;
                previousLow = currentPrice;
                Print($"New lower low detected. Count: {consecutiveLowerLows}");
            }
            else if (currentPrice > previousLow + (10 * Symbol.PipSize))
            {
                consecutiveLowerLows = 0;
                previousLow = currentPrice;
                Print("Price moved up significantly, resetting lower lows count");
            }

            Print($"Current price: {currentPrice}, Previous low: {previousLow}");
            Print($"RSI values - Current: {currentRsi:F2}, Previous: {previousRsi:F2}, Two bars ago: {twoBarsAgoRsi:F2}");
            Print($"Consecutive lower lows: {consecutiveLowerLows}");

            bool isOversold = currentRsi <= RsiOversoldLevel;
            bool rsiTurningUp = currentRsi > previousRsi && previousRsi < twoBarsAgoRsi;
            bool enoughDowntrend = consecutiveLowerLows >= 3;

            Print($"Conditions - Oversold: {isOversold}, RSI turning up: {rsiTurningUp}, Enough downtrend: {enoughDowntrend}");

            if (isOversold && rsiTurningUp && enoughDowntrend)
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