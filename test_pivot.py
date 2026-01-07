from Scripts.screenshot import get_ohlc_data, calculate_pivots
import json

ohlc = get_ohlc_data('KRPLS')
print(f"OHLC returned: {ohlc}")
print(f"OHLC type: {type(ohlc)}")

if ohlc:
    pivots = calculate_pivots(ohlc)
    print(f"\nPivots returned: {pivots}")
    print(f"Pivots type: {type(pivots)}")
    
    if pivots:
        print("\nPivots JSON:")
        print(json.dumps(pivots, indent=2))
    else:
        print("Pivots is None or False")
else:
    print("OHLC is None or False")
