# TMech.AudioNormalizer

A hobby project where I made a program that's a wrapper for using ffmpeg's loudnorm filter to normalize the loudness level of audio files. 'Why', you might ask, when other programs/scripts exist that already do the same? Because:

1: I had a 3000+ music collection of assorted file-formats I wanted to properly normalize that I have been collecting and curating for over 20 years.
2: I love programming and particularly making stuff myself.
3: It's a great learning experience, particulary doing things I hadn't done much before (working more extensively with external programs).

One of the traditional ways to normalize audio files was using metadata embedded in the files, typically via ReplayGain. This approach has fallen out of favour in later years due to several factors. One of those is you would only get normalized sound on players that support ReplayGain and another is that not all audio format supports ReplayGain metadata in the first place.

Loudness normalization on the other hand transforms the waveform (the sound) of your files and therefore works on any file giving you equal sound levels on any device or player it is played on. No metadata-tag support needed. This does mean that this is a heavy operation as each file has to be analyzed and then (re)encoded with the new loudness level.

The project is provided here in public **as-is**. It is open-source but not open-contribution. Patches, feature- and change requests are not accepted. If there are bugs I will try to fix them when my time allows.

# What it does do and what it cannot do

It is designed to work on audio-file with a single audio stream (different than channels). It currently works on **mp3, opus and aac** files only. It works on a single input directory (no recursion) and outputs the files to an output directory. The original files are left untouched.

It first strips the files of any existing ReplayGain-tags, then analyzes the current loudness level and if it's not within the loudness treshold (-16.0 LUFS +/- 1.0) it is normalized. These levels are hardcoded and cannot be changed. The files are processed concurrently 4 at a time (this also not configureable).

# What is required

You need both **ffmpeg** and **ffprobe** installed. The program will try to test if they are available on the **PATH**. If for whatever reason they are not you can always pass the location (absolute path) to where ffmpeg and ffprobe are located via a command line variable.

# How to use

`.\TMech.AudioNormalizer.exe -inputDir='somepath/to/music' -outputDir='some/output/folder' -ffmpegDir='where/ffmpeg/lives'`

The option **ffmpegDir** is optional if ffmpeg/ffprobe is available on the **PATH**.
A logfile called **AudioNormalizer.log** is generated in the output dir.

# Any plans for the future?

As far as I am concerned - aside from bugfixes - it is done. If I were to continue working on it then these could be improvements:

Maybe make more things configurable via command line variables? Such as parallel processing count, loudness threshold, what file-types to include, what bitrates to reencode the different formats in etc. This is definitely NOT trivial and would require significant time to implement and get to work properly.

Maybe make it so that you can process a directory recursively? However this implies that it will basically mimic the folder structure as well because otherwise you might get thousands of input-files spread across hundreds of folders just dumped into a single folder. And if any of the files have the same name they will be overwritten. Not exactly trivial to implement either.

# Technical details

Here's a deep dive into the stages that each file goes through.

# 1. Analysis of audio stream

The audio file is first analysed using **ffprobe** to extract the codec, sample rate, channels and bitrate.
This also immediately determines whether a file can actually be processed. If the file is corrupted or not an actual audio file (but something else renamed) this step will fail and the file is ignored.

# 2. Stripping ReplayGain tags

The audio file is then copied using **ffmpeg** where the copy is a version without any ReplayGain-tags. In the event these tags do exist it would mean that you might have a normalized file that is then still having its volume affected by the player because it read out and used those tags.

# 3. Analyzing loudnorm

After this the stripped copy of the audio file is analyzed by **ffmpeg** using the loudnorm-filter. This will determine what its current levels are and what corrections are needed to normalize it during the second pass through the filter where the normalization is actually taking place.

The target LUFS is hardcoded to -16.0 which is quite common for music and if the loudnorm analysis reveals that the file is already close to the target (between -15.0 and -17.0) then it is not normalized. The file in the output directory is just the original without the ReplayGain-tags.

# 4. Normalization

The final pass is when the audio file is actually normalized. Again the stripped copy is passed to **ffmpeg** using the loudnorm values extracted during the previous pass along with the codec, sample rate and channel-count from the first stage. This requires the file to be reencoded, and unfortunately reencoding is like taking a copy of a copy: degradation in sound quality is ineviable.

To minimize the quality loss the files are encoded using - potentially - higher settings than the original file was encoded in:

MP3  => VBR (Variable Bitrate) ~245 kbps, highest quality
OPUS => 160 kbps
AAC  => 192 kbps. In addition AAC files have their metadata moved to the beginning of the file so they are more streaming friendly (using the +faststart flag)

As a result some files might be smaller in size (particularly mp3 which are often in CBR 320 kpbs) but could also be bigger.

# AI-disclaimer

No generative AI in any form is used by this application or in making this application. Every single line of code has been handwritten by the author.