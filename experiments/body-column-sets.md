# Body Column Value Sets (Row-Pair 8/10)

## Goal
Identify column-level value restrictions in the shared prefix of row-pair 8 and
the lone row-pair 10 band to see if any columns are tightly constrained.

## Method
- Use motif-based header segmentation (header length = 6).
- For row-pair 8, read column-ordered glyph pairs (top * 5 + bottom) for
  columns 0..14 (all six row-pair 8 messages share these columns).
- For row-pair 10, read the column-ordered pairs (message 4 only).
- Record the set of observed values per column.

## Row-pair 8 column sets (coverage = 6)
Format: column index -> size, values.

0 -> size 6: 1,4,5,7,8,10  
1 -> size 5: 1,10,12,14,18  
2 -> size 6: 2,5,7,12,20,21  
3 -> size 5: 1,2,11,14,17  
4 -> size 5: 0,8,12,14,16  
5 -> size 4: 0,7,21,22  
6 -> size 6: 0,1,5,6,10,11  
7 -> size 4: 5,10,17,22  
8 -> size 6: 1,2,10,17,20,23  
9 -> size 4: 0,5,11,13  
10 -> size 6: 1,2,10,13,19,23  
11 -> size 3: 5,11,21  
12 -> size 6: 4,5,6,11,14,15  
13 -> size 6: 0,4,8,10,13,23  
14 -> size 6: 1,5,6,7,17,22  

Intersection across columns 0..14: **empty**

## Row-pair 10 column sets (coverage = 1)
0: 0  
1: 24  
2: 7  
3: 13  
4: 20  
5: 1  
6: 5  
7: 11  
8: 15  
9: 4  

## Interpretation
Row-pair 8 columns show moderate restriction (3-6 values per column) but no
global shared value across all columns. Column 11 is the tightest (3 values),
and columns 5/7/9 show size 4 sets. This suggests structured constraints, but
not fixed symbols.

## Next Steps
- Compare these column sets between East vs West subsets to see if the
  restriction sets differ by side.
- Test whether column sets correlate with the header lead trigram or motif type.
