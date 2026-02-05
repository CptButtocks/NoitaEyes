Eye Message Music

This is by no means a translation or interpretation of the eye messages. But the theory proposed by finngamesoo\#0810 ([kantele Eyes](https://docs.google.com/document/d/1y1VpFy1U5dP-65IsgFO4oPIiuMjPswL9YBtgKgK3FE4/edit)) intrigued my curiosity.

I decided to try converting the eye messages to musical notes through a couple different methods to see what the messages would sound like. I found it fun to do and some of the resulting tunes are pretty trippy.

The following sections cover my thought process and method. You can find the accompanying SCV, MIDI, and MP3 files in the same drive you found this document. Hopefully you find the results as intriguing as I do. My personal favorite are the “a=4 chords”. With some styling and manual playing, they could sound pretty creepy.

If you have any questions or suggestions, feel free to poke me on discord at Fryke\#0746

“Trigrams Standard”  
The CSVs for these translations are based on reading the trigrams in the generally accepted way. The CSV files are then run through a Python script that reads the ‘eye values’, converts them to notes, and writes them to a MIDI file.

The tricky part is deducing what notes to assign to what eye values. On the wiki ([https://noita.fandom.com/wiki/Kantele](https://noita.fandom.com/wiki/Kantele)) the notes are ordered as “A, D, D\#, E, G”. This is also the order they appear in-game within the tree.

But interestingly enough, the ‘A’ note is an octave higher than it should be in order to match that order. If you follow the notes by pitch, the order should be “D, D\#, E, G, A”. For the sake of curiosity, I tried both.

Once the MIDI files were properly generated, I ran them through a DAW to make the MP3s.

I had a thought to try overlapping the various tracks with each other, but the resulting combinations were pretty much random garble. The only interesting bit is that they very clearly highlight the similarities between the various eye messages.

“a=0” Files  
These files are for the “A, D, D\#, E, G” sequence of notes. Each trigram is read into a series of 3 values and then those values are directly translated to notes. All triads are then played right after each other with no breaks in between. I found the tune to feel more cohesive this way.

“a=4” Files  
These files are for the “D, D\#, E, G, A” sequence of notes. Trigrams are processed the same way as the “a=0” files.

“a=x Chords”  
With these files, I interpreted the trigrams as chords of three notes each. The problem with this approach is that trigrams may contain duplicates, which makes no sense for a chord. So, I shifted the notes an octave based on their position in the triad.

The first note of a triad is placed in the 3rd octave, the second note in the 4th octave, and the third note in the 5th octave.

In addition, I put some space in between the chords for emphasis.

“Trigram Sums”  
The CSVs for these translations are based on the sum of each trigram. Since trigrams can have a total of 13 possible summed values (0 \- 12), they can be directly mapped to an inclusive octave of notes.

Since it is fairly easy to transpose the MIDI information so the sequence of notes starts on whatever note you want, I opted to create just one selection of notes. The one I put together uses the scale from C3 to C4.

The notes are played one right after another and the tunes are much shorter since each trigram only represents a single note.

Note that this method does NOT respect the kantele notes.

“Linear”  
The CSVs for these translations are based on reading the eye messages linearly, without regard for the trigram pattern. Lines are read from left to right, top to bottom, just like normal text.

They are then processed and organized just like the “Trigrams Standard” files with both note sequences and chords.