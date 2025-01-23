using Ionic.Zlib;
using cswlib.cswlib.util;

namespace cswlib.cswlib.io.serialization.types {
    public class CompressedData {

        enum Versions
        {
            CompressedDataInitial = 1,
            
            LatestPlusOne
        }

        byte[] data = new byte[0];

        private readonly int ChunkSize = 0x8000;

        public CompressedData(bool serialize = true) {
            if (serialize)
                Serialize();
        }

        public CompressedData(byte[] data, bool serialize = true)
        {
            this.data = data;
            if (serialize)
                Serialize();
        }

        public Serializer.SERIALIZER_RESULT Serialize()
        {
            bool _IsWriting = Serializer.IsWriting();
            short chunks = (short)Math.Ceiling((double)data.Length / (double)ChunkSize);
            
            short currentVersion = (int)Versions.LatestPlusOne - 1;
            Serializer.Serialize(ref currentVersion, compress:false); // basically unused but whatever
            if (currentVersion > (int)Versions.LatestPlusOne - 1)
            {
                Util.err_printf("File too new!\n");
                return Serializer.SERIALIZER_RESULT.FORMAT_TOO_NEW;
            }

            Serializer.Serialize(ref chunks, compress:false);

            ushort[] compressed = new ushort[chunks];
            ushort[] decompressed = new ushort[chunks];

            if (chunks == 0 && !_IsWriting)
            {
                data = new byte[0];
                Util.err_printf("Compressed data is invalid!\n");
                return Serializer.SERIALIZER_RESULT.DECOMPRESSION_FAIL; // invalid state!
            }
            else if (chunks == 0)
            {
                Util.wrn_printf("Attempted to compress literally nothing. Why?\n");
                return Serializer.SERIALIZER_RESULT.COMPRESSION_FAIL;
            }

            int FinalChunkSize = data.Length % ChunkSize;

            byte[][] chunkedData = new byte[chunks][];

            byte[][] compressedData = new byte[chunks][];

            if (_IsWriting) 
            {
                for (int i = 0; i < chunks; i++) // split and compress data
                {
                    int CurrentChunkSize = ChunkSize;

                    if (i == chunks - 1)
                        CurrentChunkSize = FinalChunkSize;

                    chunkedData[i] = new byte[CurrentChunkSize];
                    Array.Copy(data, i * CurrentChunkSize, chunkedData[i], 0, CurrentChunkSize);

                    compressedData[i] = ZlibStream.CompressBuffer(chunkedData[i]);

                    decompressed[i] = (ushort)chunkedData[i].Length;
                    compressed[i] = (ushort)compressedData[i].Length;
                }
            }

            int decompressedSize = 0;
            for (int i = 0; i < chunks; ++i)
            {
                Serializer.Serialize(ref compressed[i], compress: false);
                Serializer.Serialize(ref decompressed[i], compress: false);
                //Debug.LogFormat("Chunk {0}: compressed size is {1} and decompressed size is {2}", i, compressed[i], decompressed[i]);
                decompressedSize += decompressed[i];
            }

            if (!_IsWriting)
                data = new byte[decompressedSize];
            int currentOffset = 0;
            for (int i = 0; i < chunks; i++)
            {
                byte[] deflatedData = new byte[compressed[i]];

                if (_IsWriting)
                    Array.Copy(compressedData[i], deflatedData, compressedData[i].Length);

                for (int j = 0; j < compressed[i]; j++)
                {
                    Serializer.Serialize(ref deflatedData[j], compress: false);
                }

                if (_IsWriting) // from now on it's decompressing stuff
                    continue;

                byte[] inflatedData;
                if (compressed[i] == decompressed[i])
                {
                    inflatedData = deflatedData;
                }
                else
                {
                    inflatedData = ZlibStream.UncompressBuffer(deflatedData);
                    if (inflatedData == null)
                    {
                        data = new byte[0];
                        return Serializer.SERIALIZER_RESULT.DECOMPRESSION_FAIL;
                    }
                }

                Array.Copy(inflatedData, 0, data, currentOffset, (int)decompressed[i]);
                currentOffset += decompressed[i];
            }
            return Serializer.SERIALIZER_RESULT.OK;
        }

        public static implicit operator byte[](CompressedData d) => d.data;

    }
}