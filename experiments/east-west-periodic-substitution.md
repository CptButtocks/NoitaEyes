# East–West Periodic Substitution (Index mod N)

## Goal
Test whether an index-periodic substitution (rotating key) can explain East/West
trigram mappings after anchoring on the shared `[66,5]` motif.

## Method
- Use anchored alignment (anchor `[66,5]`) for each East/West pair.
- For each modulus N (2..14), build mappings keyed by `(index mod N, East trigram)`.
- Count conflicts where one key maps to multiple West trigrams.

## Results
Best modulus (lowest conflicts): **N = 12**

```
mod2:  conflicts=68,  mappings=314
mod3:  conflicts=55,  mappings=340
mod4:  conflicts=39,  mappings=361
mod5:  conflicts=44,  mappings=363
mod6:  conflicts=35,  mappings=371
mod7:  conflicts=31,  mappings=372
mod8:  conflicts=22,  mappings=386
mod9:  conflicts=26,  mappings=384
mod10: conflicts=24,  mappings=385
mod11: conflicts=17,  mappings=391
mod12: conflicts=16,  mappings=395
mod13: conflicts=16,  mappings=392
mod14: conflicts=16,  mappings=392
```

## Interpretation
Periodic substitution reduces conflicts but does **not** eliminate them; even at
the best modulus (12), 16 conflicts remain across the aligned pairs. This
suggests any cipher state depends on more than a simple positional period.

## Next Steps
- Increase modulus range (e.g., 15–30) to check longer periods.
- Try per-message periodic maps or mixed anchors to test robustness.

## Full sweep with baseline (mod 2–60)
Normalization: conflict rate = conflicts / mappings. Baseline = shuffled mappings
with the same index residues (200 samples, seed=1337).

Best by lowest conflict rate: **mod 57** (rate 0.0447).  
Best by improvement vs baseline mean: **mod 8** (delta 0.0449).

```
mod2:  conf=114,map=149,rate=0.7651,baseMean=119.19,baseRate=0.8000,baseMin=115,baseMax=120
mod3:  conf=109,map=195,rate=0.5590,baseMean=114.25,baseRate=0.5859,baseMin=110,baseMax=115
mod4:  conf=105,map=231,rate=0.4545,baseMean=113.95,baseRate=0.4933,baseMin=111,baseMax=115
mod5:  conf=97,map=257,rate=0.3774,baseMean=104.20,baseRate=0.4054,baseMin=101,baseMax=105
mod6:  conf=91,map=268,rate=0.3396,baseMean=97.13,baseRate=0.3624,baseMin=94,baseMax=98
mod7:  conf=86,map=278,rate=0.3094,baseMean=93.03,baseRate=0.3346,baseMin=89,baseMax=94
mod8:  conf=83,map=289,rate=0.2872,baseMean=95.97,baseRate=0.3321,baseMin=91,baseMax=97
mod9:  conf=74,map=297,rate=0.2492,baseMean=83.27,baseRate=0.2804,baseMin=81,baseMax=84
mod10: conf=67,map=310,rate=0.2161,baseMean=74.28,baseRate=0.2396,baseMin=71,baseMax=75
mod11: conf=68,map=312,rate=0.2179,baseMean=76.23,baseRate=0.2443,baseMin=73,baseMax=77
mod12: conf=55,map=329,rate=0.1672,baseMean=63.34,baseRate=0.1925,baseMin=61,baseMax=64
mod13: conf=52,map=331,rate=0.1571,baseMean=63.27,baseRate=0.1911,baseMin=61,baseMax=64
mod14: conf=53,map=331,rate=0.1601,baseMean=64.32,baseRate=0.1943,baseMin=61,baseMax=65
mod15: conf=52,map=338,rate=0.1538,baseMean=59.37,baseRate=0.1757,baseMin=57,baseMax=60
mod16: conf=60,map=332,rate=0.1807,baseMean=71.10,baseRate=0.2142,baseMin=67,baseMax=72
mod17: conf=54,map=334,rate=0.1617,baseMean=62.37,baseRate=0.1867,baseMin=60,baseMax=63
mod18: conf=44,map=343,rate=0.1283,baseMean=53.34,baseRate=0.1555,baseMin=50,baseMax=54
mod19: conf=43,map=353,rate=0.1218,baseMean=51.40,baseRate=0.1456,baseMin=49,baseMax=52
mod20: conf=41,map=350,rate=0.1171,baseMean=50.34,baseRate=0.1438,baseMin=47,baseMax=51
mod21: conf=38,map=350,rate=0.1086,baseMean=47.51,baseRate=0.1357,baseMin=45,baseMax=48
mod22: conf=39,map=351,rate=0.1111,baseMean=49.44,baseRate=0.1408,baseMin=47,baseMax=50
mod23: conf=36,map=359,rate=0.1003,baseMean=43.51,baseRate=0.1212,baseMin=41,baseMax=44
mod24: conf=36,map=356,rate=0.1011,baseMean=45.47,baseRate=0.1277,baseMin=42,baseMax=46
mod25: conf=27,map=368,rate=0.0734,baseMean=33.55,baseRate=0.0912,baseMin=31,baseMax=34
mod26: conf=29,map=361,rate=0.0803,baseMean=40.51,baseRate=0.1122,baseMin=39,baseMax=41
mod27: conf=38,map=353,rate=0.1076,baseMean=49.34,baseRate=0.1398,baseMin=45,baseMax=50
mod28: conf=31,map=362,rate=0.0856,baseMean=39.51,baseRate=0.1091,baseMin=37,baseMax=40
mod29: conf=37,map=357,rate=0.1036,baseMean=43.48,baseRate=0.1218,baseMin=39,baseMax=44
mod30: conf=39,map=354,rate=0.1102,baseMean=45.49,baseRate=0.1285,baseMin=43,baseMax=46
mod31: conf=32,map=361,rate=0.0886,baseMean=39.59,baseRate=0.1097,baseMin=37,baseMax=40
mod32: conf=34,map=361,rate=0.0942,baseMean=44.52,baseRate=0.1233,baseMin=43,baseMax=45
mod33: conf=28,map=365,rate=0.0767,baseMean=37.64,baseRate=0.1031,baseMin=35,baseMax=38
mod34: conf=33,map=361,rate=0.0914,baseMean=41.55,baseRate=0.1151,baseMin=39,baseMax=42
mod35: conf=27,map=365,rate=0.0740,baseMean=36.57,baseRate=0.1002,baseMin=33,baseMax=37
mod36: conf=28,map=366,rate=0.0765,baseMean=37.53,baseRate=0.1025,baseMin=35,baseMax=38
mod37: conf=28,map=370,rate=0.0757,baseMean=35.55,baseRate=0.0961,baseMin=32,baseMax=36
mod38: conf=28,map=369,rate=0.0759,baseMean=37.51,baseRate=0.1017,baseMin=35,baseMax=38
mod39: conf=25,map=370,rate=0.0676,baseMean=32.71,baseRate=0.0884,baseMin=30,baseMax=33
mod40: conf=29,map=367,rate=0.0790,baseMean=38.55,baseRate=0.1050,baseMin=37,baseMax=39
mod41: conf=21,map=375,rate=0.0560,baseMean=29.68,baseRate=0.0791,baseMin=28,baseMax=30
mod42: conf=26,map=369,rate=0.0705,baseMean=35.62,baseRate=0.0965,baseMin=33,baseMax=36
mod43: conf=25,map=372,rate=0.0672,baseMean=33.66,baseRate=0.0905,baseMin=31,baseMax=34
mod44: conf=25,map=369,rate=0.0678,baseMean=34.55,baseRate=0.0936,baseMin=33,baseMax=35
mod45: conf=23,map=374,rate=0.0615,baseMean=30.71,baseRate=0.0821,baseMin=28,baseMax=31
mod46: conf=21,map=376,rate=0.0559,baseMean=29.58,baseRate=0.0787,baseMin=27,baseMax=30
mod47: conf=27,map=369,rate=0.0732,baseMean=34.55,baseRate=0.0936,baseMin=32,baseMax=35
mod48: conf=26,map=371,rate=0.0701,baseMean=34.57,baseRate=0.0932,baseMin=32,baseMax=35
mod49: conf=27,map=369,rate=0.0732,baseMean=33.59,baseRate=0.0910,baseMin=30,baseMax=34
mod50: conf=22,map=375,rate=0.0587,baseMean=28.66,baseRate=0.0764,baseMin=26,baseMax=29
mod51: conf=23,map=372,rate=0.0618,baseMean=32.63,baseRate=0.0877,baseMin=30,baseMax=33
mod52: conf=20,map=377,rate=0.0531,baseMean=29.63,baseRate=0.0786,baseMin=28,baseMax=30
mod53: conf=26,map=371,rate=0.0701,baseMean=34.58,baseRate=0.0932,baseMin=32,baseMax=35
mod54: conf=20,map=376,rate=0.0532,baseMean=28.65,baseRate=0.0762,baseMin=26,baseMax=29
mod55: conf=27,map=370,rate=0.0730,baseMean=34.60,baseRate=0.0935,baseMin=32,baseMax=35
mod56: conf=22,map=373,rate=0.0590,baseMean=30.62,baseRate=0.0821,baseMin=28,baseMax=31
mod57: conf=17,map=380,rate=0.0447,baseMean=25.70,baseRate=0.0676,baseMin=24,baseMax=26
mod58: conf=21,map=375,rate=0.0560,baseMean=30.64,baseRate=0.0817,baseMin=28,baseMax=31
mod59: conf=25,map=373,rate=0.0670,baseMean=33.55,baseRate=0.0899,baseMin=31,baseMax=34
mod60: conf=21,map=375,rate=0.0560,baseMean=29.68,baseRate=0.0791,baseMin=28,baseMax=30
```

## Alternate anchor: [5,49,75,54]
Anchor present only in messages 3–8, so the sweep uses E3/W3 and E4/W4 pairs.

Best by lowest conflict rate: **mod 48** (rate 0.0330).  
Best by improvement vs baseline mean: **mod 8** (delta 0.0418).

```
mod2:  conf=62,map=116,rate=0.5345,baseMean=64.61,baseRate=0.5569,baseMin=62,baseMax=65
mod3:  conf=54,map=144,rate=0.3750,baseMean=56.62,baseRate=0.3932,baseMin=55,baseMax=57
mod4:  conf=47,map=157,rate=0.2994,baseMean=52.56,baseRate=0.3348,baseMin=50,baseMax=53
mod5:  conf=41,map=164,rate=0.2500,baseMean=44.62,baseRate=0.2720,baseMin=43,baseMax=45
mod6:  conf=43,map=166,rate=0.2590,baseMean=46.48,baseRate=0.2800,baseMin=43,baseMax=47
mod7:  conf=32,map=176,rate=0.1818,baseMean=35.62,baseRate=0.2024,baseMin=33,baseMax=36
mod8:  conf=29,map=181,rate=0.1602,baseMean=36.56,baseRate=0.2020,baseMin=33,baseMax=37
mod9:  conf=29,map=185,rate=0.1568,baseMean=33.66,baseRate=0.1820,baseMin=32,baseMax=34
mod10: conf=31,map=181,rate=0.1713,baseMean=34.63,baseRate=0.1913,baseMin=32,baseMax=35
mod11: conf=24,map=191,rate=0.1257,baseMean=29.64,baseRate=0.1552,baseMin=28,baseMax=30
mod12: conf=21,map=195,rate=0.1077,baseMean=26.64,baseRate=0.1366,baseMin=24,baseMax=27
mod13: conf=22,map=194,rate=0.1134,baseMean=25.70,baseRate=0.1325,baseMin=23,baseMax=26
mod14: conf=23,map=193,rate=0.1192,baseMean=27.69,baseRate=0.1435,baseMin=26,baseMax=28
mod15: conf=21,map=194,rate=0.1082,baseMean=24.75,baseRate=0.1276,baseMin=23,baseMax=25
mod16: conf=20,map=197,rate=0.1015,baseMean=25.66,baseRate=0.1303,baseMin=23,baseMax=26
mod17: conf=21,map=195,rate=0.1077,baseMean=24.68,baseRate=0.1265,baseMin=22,baseMax=25
mod18: conf=20,map=197,rate=0.1015,baseMean=25.73,baseRate=0.1306,baseMin=24,baseMax=26
mod19: conf=14,map=204,rate=0.0686,baseMean=19.75,baseRate=0.0968,baseMin=17,baseMax=20
mod20: conf=18,map=200,rate=0.0900,baseMean=23.66,baseRate=0.1183,baseMin=21,baseMax=24
mod21: conf=14,map=203,rate=0.0690,baseMean=18.68,baseRate=0.0920,baseMin=17,baseMax=19
mod22: conf=15,map=201,rate=0.0746,baseMean=20.82,baseRate=0.1036,baseMin=19,baseMax=21
mod23: conf=19,map=199,rate=0.0955,baseMean=23.77,baseRate=0.1195,baseMin=22,baseMax=24
mod24: conf=13,map=204,rate=0.0637,baseMean=18.77,baseRate=0.0920,baseMin=16,baseMax=19
mod25: conf=8,map=209,rate=0.0383,baseMean=12.90,baseRate=0.0617,baseMin=11,baseMax=13
mod26: conf=11,map=206,rate=0.0534,baseMean=15.86,baseRate=0.0770,baseMin=14,baseMax=16
mod27: conf=15,map=204,rate=0.0735,baseMean=19.71,baseRate=0.0966,baseMin=18,baseMax=20
mod28: conf=11,map=206,rate=0.0534,baseMean=15.79,baseRate=0.0766,baseMin=14,baseMax=16
mod29: conf=13,map=206,rate=0.0631,baseMean=17.87,baseRate=0.0867,baseMin=17,baseMax=18
mod30: conf=19,map=198,rate=0.0960,baseMean=22.75,baseRate=0.1149,baseMin=20,baseMax=23
mod31: conf=13,map=205,rate=0.0634,baseMean=17.83,baseRate=0.0870,baseMin=16,baseMax=18
mod32: conf=12,map=206,rate=0.0583,baseMean=17.80,baseRate=0.0864,baseMin=15,baseMax=18
mod33: conf=11,map=207,rate=0.0531,baseMean=14.81,baseRate=0.0716,baseMin=13,baseMax=15
mod34: conf=15,map=203,rate=0.0739,baseMean=18.75,baseRate=0.0923,baseMin=17,baseMax=19
mod35: conf=14,map=202,rate=0.0693,baseMean=18.80,baseRate=0.0930,baseMin=16,baseMax=19
mod36: conf=13,map=204,rate=0.0637,baseMean=18.77,baseRate=0.0920,baseMin=17,baseMax=19
mod37: conf=10,map=209,rate=0.0478,baseMean=14.86,baseRate=0.0711,baseMin=13,baseMax=15
mod38: conf=11,map=207,rate=0.0531,baseMean=16.82,baseRate=0.0813,baseMin=15,baseMax=17
mod39: conf=11,map=208,rate=0.0529,baseMean=13.81,baseRate=0.0664,baseMin=12,baseMax=14
mod40: conf=11,map=207,rate=0.0531,baseMean=16.80,baseRate=0.0811,baseMin=15,baseMax=17
mod41: conf=8,map=211,rate=0.0379,baseMean=12.79,baseRate=0.0606,baseMin=11,baseMax=13
mod42: conf=10,map=209,rate=0.0478,baseMean=14.80,baseRate=0.0708,baseMin=13,baseMax=15
mod43: conf=12,map=207,rate=0.0580,baseMean=16.80,baseRate=0.0812,baseMin=15,baseMax=17
mod44: conf=10,map=207,rate=0.0483,baseMean=15.86,baseRate=0.0766,baseMin=14,baseMax=16
mod45: conf=8,map=211,rate=0.0379,baseMean=12.85,baseRate=0.0609,baseMin=12,baseMax=13
mod46: conf=9,map=210,rate=0.0429,baseMean=13.84,baseRate=0.0659,baseMin=12,baseMax=14
mod47: conf=8,map=210,rate=0.0381,baseMean=12.81,baseRate=0.0610,baseMin=11,baseMax=13
mod48: conf=7,map=212,rate=0.0330,baseMean=11.85,baseRate=0.0559,baseMin=11,baseMax=12
mod49: conf=12,map=205,rate=0.0585,baseMean=15.81,baseRate=0.0771,baseMin=14,baseMax=16
mod50: conf=8,map=211,rate=0.0379,baseMean=12.87,baseRate=0.0610,baseMin=11,baseMax=13
mod51: conf=10,map=207,rate=0.0483,baseMean=15.84,baseRate=0.0765,baseMin=14,baseMax=16
mod52: conf=8,map=210,rate=0.0381,baseMean=13.81,baseRate=0.0658,baseMin=12,baseMax=14
mod53: conf=12,map=207,rate=0.0580,baseMean=16.82,baseRate=0.0812,baseMin=15,baseMax=17
mod54: conf=8,map=211,rate=0.0379,baseMean=12.79,baseRate=0.0606,baseMin=11,baseMax=13
mod55: conf=9,map=210,rate=0.0429,baseMean=13.87,baseRate=0.0660,baseMin=12,baseMax=14
mod56: conf=9,map=208,rate=0.0433,baseMean=13.85,baseRate=0.0666,baseMin=11,baseMax=14
mod57: conf=7,map=212,rate=0.0330,baseMean=11.84,baseRate=0.0558,baseMin=10,baseMax=12
mod58: conf=10,map=209,rate=0.0478,baseMean=14.82,baseRate=0.0709,baseMin=12,baseMax=15
mod59: conf=9,map=210,rate=0.0429,baseMean=13.79,baseRate=0.0657,baseMin=12,baseMax=14
mod60: conf=9,map=210,rate=0.0429,baseMean=13.81,baseRate=0.0658,baseMin=12,baseMax=14
```

### Interpretation
The alternate anchor uses fewer pairs (E3/W3 and E4/W4 only), so the sweep has
less data and is more sensitive to dilution effects. The best conflict-rate
modulus shifts to 48 (and ties with 57), while the strongest improvement over
baseline remains at mod 8. This suggests that any periodic structure is weak
and unstable across anchors; the apparent “best” mod depends on the anchor and
data volume rather than revealing a consistent cipher period.
