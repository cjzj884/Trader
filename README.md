# Trader
A cryptotrader bot. Currently targeting the GDAX market, due to their lower fees.

## Current state
Monitors the GDAX ETH-USD ticker and makes hypothetical trades based on in-memory "wallets" with hardcoded start values.
Still needs some tuning, so I haven't yet implemented the actual GDAX buy and sell API calls.

See the Github issues for more details on what's coming next!

## Algorithm 
First, checks the start and end price over a 20m window to establish if the market is currently up or down.

If the market is going up, but encounters a downturn that's at least 10% of the current upturn, we assume we've gone bearish (and sell).
Conversely, if the market is going down, but encounters an upturn that's at least 10% of the current downturn, we assume we've gone bullish (and buy).

All turns less than 2% of the total value are ignored as noise.

There is also a time-decay on the swing-threshold - 10% linearaly down to 5% over 1 day. It gets more eager the longer the price has remained stagnant. 
I'm not sure if this is actually necessary, as it's never gone more than a day without trading and resetting. Or maybe it also just needs tuning.

For examples of when the algorithm might choose to buy or sell, refer to the [unit tests]( https://github.com/tevert/Trader/blob/master/Trader.Tests/TraderTests.cs).
