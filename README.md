# Trader
A stupid buggy cryptotrader bot. Currently targeting the GDAX market, due to their lower fees.

## Current state
Monitors the GDAX ETH-USD ticker and makes hypothetical trades based on in-memory "wallets" with hardcoded start values.
Still needs some tuning (not to mention unit tests), so I'm still not even going to bother hooking up real buy/sell API commands.

## Algorithm 
First, checks the start and end price over a 20m window to establish if the market is currently up or down.

If the market is going up, but encounters a downturn that's at least 20% of the current upturn, we assume we've gone bearish (and sell).
Conversely, if the market is going down, but encounters an upturn that's at least 20% of the current downturn, we assume we've gone bullish (and buy).

All turns less than 0.5% are ignored as noise. This might need further tuning to get the appropriate aggressiveness relative to exchange fees and uncertainty. 

There is also a time-decay on the swing-threshold - 20% linear down to 5% over 5 days. It gets more eager the longer the price has remained stagnant. 
I'm not sure if this is actually necessary, as it's never gone more than a day without trading and resetting. Or maybe it also just needs tuning.