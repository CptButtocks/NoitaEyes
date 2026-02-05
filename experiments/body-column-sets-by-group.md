# Row-Pair 8 Column Sets by Group

## Goal
Compare column value restrictions in row-pair 8 bands for East vs West and
header motif groups.

## Method
- Use motif-based header segmentation (header length = 6).
- Extract row-pair 8 column sequences (top * 5 + bottom) for each message.
- For each group, compute column value sets using only columns shared by all
  messages in that group (coverage = group size).

## East (ids 0,2,4,6,8; row-pair 8 present in 4)
Coverage = 4, columns 0..14:

0: 1,4,7,8  
1: 1,14,18  
2: 5,7,12,21  
3: 1,2,14  
4: 0,12,14,16  
5: 0,7,21,22  
6: 0,6,10,11  
7: 10,17,22  
8: 1,2,17,23  
9: 5,13  
10: 1,2,10,13  
11: 5,11  
12: 4,6,14,15  
13: 4,8,13,23  
14: 1,6,7,22  

## West (ids 1,3,5,7; row-pair 8 present in 2)
Coverage = 2, columns 0..23:

0: 5,10  
1: 10,12  
2: 2,20  
3: 11,17  
4: 8,12  
5: 21  
6: 1,5  
7: 5,10  
8: 10,20  
9: 0,11  
10: 19,23  
11: 11,21  
12: 5,11  
13: 0,10  
14: 5,17  
15: 2,6  
16: 5,19  
17: 17,22  
18: 6  
19: 10,24  
20: 0,3  
21: 5,14  
22: 16,20  
23: 1,22  

## Motif B (ids 3,4,5,6,7,8; row-pair 8 present in 5)
Coverage = 5, columns 0..14:

0: 4,5,7,8,10  
1: 1,10,12,14,18  
2: 2,5,7,12,20  
3: 1,2,11,14,17  
4: 8,12,14,16  
5: 0,21,22  
6: 1,5,6,10,11  
7: 5,10,17  
8: 1,2,10,20,23  
9: 0,5,11,13  
10: 1,10,13,19,23  
11: 5,11,21  
12: 4,5,11,14,15  
13: 0,8,10,13,23  
14: 5,6,7,17,22  

## Motif C (ids 0,1,2; row-pair 8 present in 1)
Coverage = 1, columns 0..20 (single message):

0: 1  
1: 18  
2: 21  
3: 14  
4: 0  
5: 7  
6: 0  
7: 22  
8: 17  
9: 5  
10: 2  
11: 5  
12: 6  
13: 4  
14: 1  
15: 13  
16: 2  
17: 2  
18: 8  
19: 11  
20: 21  

## Interpretation
West column sets are much tighter (mostly size 2, with two size-1 columns),
while East sets are broader (size 2-4). Motif B sets resemble East, while Motif C
cannot be generalized (single message). This points to side- or motif-specific
constraints in the row-pair 8 payload.

## Next Steps
- Compute per-column overlap/Jaccard similarity between East and West sets to
  quantify divergence.
- Test whether the lead trigram (index 0) predicts which column set family is
  used in the row-pair 8 band.
