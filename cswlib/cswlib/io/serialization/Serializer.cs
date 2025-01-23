using System.Text;
using System.Reflection;
using cswlib.cswlib.resource;
using cswlib.cswlib.io.serialization.extensions;

// TODO: FIX THIS FILE
// I don't like pushing errors.

// Credit to torutheredfox for providing the cswlib serializer basis
namespace cswlib.cswlib.io.serialization
{
    public static class Serializer
    {
        public static class COMPRESSION_FLAG
        {
            public static byte USE_COMPRESSED_INTEGERS = 1 << 0;
            public static byte USE_COMPRESSED_VECTORS = 1 << 1;
            public static byte USE_COMPRESSED_MATRICES = 1 << 2;
        }

        public class serializer
        {
            public int DataVersion = (int)Resource.Versions.LatestPlusOne - 1;
            public short branchID = 0;
            public short branchRevision = 0;
            public byte compressionFlags = 0;
            public BinaryWriter writer; // Only set one of them. This will determine whether the current op is writing or reading.
            public BinaryReader reader;
        };

        public enum SERIALIZER_RESULT
        {
            OK,
            GENERIC_ERROR,
            EXCESSIVE_DATA,
            INSUFFICIENT_DATA,
            EXCESSIVE_ALLOCATIONS,
            FORMAT_TOO_NEW,
            FORMAT_TOO_OLD,
            COULDNT_OPEN_FILE,
            FILEIO_FAILURE,
            NETWORK_FAILURE,
            NOT_IMPLEMENTED,
            COULDNT_GET_GUID,
            UNINITIALISED,
            NAN,
            INVALID,
            RESOURCE_IN_WRONG_STATE,
            OUT_OF_GFX_MEMORY,
            OUT_OF_SYNC,
            DECOMPRESSION_FAIL,
            COMPRESSION_FAIL,
            APPLICATION_QUITTING,
            OUT_OF_MEM,
            JOB_CANCELLED,
            NULL_POINTER,
        }

        public class CompressionFlag
        {
            public static byte USE_COMPRESSED_INTEGERS = 1;
            public static byte USE_COMPRESSED_VECTORS = 2;
            public static byte USE_COMPRESSED_MATRICES = 4;
        }

        public static class BRANCH_ID
        {
            public static readonly short Leerdammer = 0x4c44;
        }

        public static int CompressionFlags = 0;

        public static serializer SerializerInfo = new Serializer.serializer();

        // IO helper functions

        public static SERIALIZER_RESULT SerializeToResource(string path, Func<SERIALIZER_RESULT> serializeFunc, Resource.ResourceType resourceType, object targetObject, bool isWriting)
        {
            FileDB.Entry entry = FileDB.Instance.GetFileEntryByPath(path);
            if (entry == null)
            {
                Debug.LogErrorFormat("File {0} not found!", path);
                return SERIALIZER_RESULT.COULDNT_GET_GUID;
            }
            return SerializeToResource(entry.guid, serializeFunc, resourceType, targetObject, isWriting);
        }

        public static SERIALIZER_RESULT SerializeToResource(uint guid, Func<SERIALIZER_RESULT> serializeFunc, Resource.ResourceType resourceType, object targetObject, bool isWriting) {
            Serializer.SERIALIZER_RESULT result = Serializer.SERIALIZER_RESULT.OK;
            Serializer.CloseStream();

            if (isWriting)
            {
                MemoryStream ms = new MemoryStream();
                Serializer.SetBinaryWriter(new BinaryWriter(ms));
                result = serializeFunc();
                ms.Close();
                byte[] data = ms.ToArray();
                Serializer.CloseStream();

                if (result != Serializer.SERIALIZER_RESULT.OK)
                    return result;

                Serializer.SetBinaryWriter(new BinaryWriter(new FileStream(FileDB.Instance.GetFileEntryByGUID(guid).path, FileMode.Create)));
                Resource resource = new Resource(resourceType, Resource.SerializationMethod.Binary, data);
                Serializer.CloseStream();
            }
            else
            {
                string path = "data.farc";
                long offset = 0;
                long size = 0;
                bool readingFromFileSystem = false;
                FileDB.Entry entry = FileDB.Instance.GetFileEntryByGUID(guid);

                if (entry == null)
                {
                    Debug.LogErrorFormat("File g{0} not found!", guid);
                    return SERIALIZER_RESULT.COULDNT_OPEN_FILE;
                }

                readingFromFileSystem = !FileDB.Instance.hashToFarcMap.ContainsKey(entry.sha1); // TODO: determine this dynamically
                if (readingFromFileSystem)
                {
                    path = FileDB.Instance.GetFileEntryByGUID(guid).path;
                } else
                {
                    int farcIndex = FileDB.Instance.hashToFarcMap[entry.sha1];
                    path = FileDB.farcsToProcess[farcIndex];
                    offset = FileDB.Instance.farcs[farcIndex].HashToEntryMap[entry.sha1].offset;
                    size = FileDB.Instance.farcs[farcIndex].HashToEntryMap[entry.sha1].size;
                    Util.dbg_printf("Reading from FARC\n");
                }
                Serializer.SetBinaryReader(new BinaryReader(new FileStream(path, FileMode.Open)));
                
                if (readingFromFileSystem) {
                    offset = 0;
                    size = GetBaseStream().Length;
                }

                Resource resource = new Resource(resourceType, offset, size);
                byte[] cdata = resource;
                Serializer.CloseStream();

                if (result != Serializer.SERIALIZER_RESULT.OK)
                    return result;

                MemoryStream ms = new MemoryStream(cdata);
                BinaryReader reader = new BinaryReader(ms);
                Serializer.SetBinaryReader(reader);
                if (resource.serializationMethod == Resource.SerializationMethod.Text)
                {
                    //Debug.Log(Encoding.UTF8.GetString(cdata));
                    readTextResource(reader, targetObject);
                }
                else
                    result = serializeFunc();
                ms.Close();
                Serializer.CloseStream();
            }
            return result;
        }

        public static SERIALIZER_RESULT SerializeToResource(Resource resource, Func<SERIALIZER_RESULT> serializeFunc, Resource.ResourceType resourceType, object targetObject)
        {
            Serializer.SERIALIZER_RESULT result = Serializer.SERIALIZER_RESULT.OK;
            Serializer.CloseStream();
            if (result != Serializer.SERIALIZER_RESULT.OK)
                return result;
            MemoryStream ms = new MemoryStream(resource);
            BinaryReader reader = new BinaryReader(ms);
            Serializer.SetBinaryReader(reader);
            if (resource.serializationMethod == Resource.SerializationMethod.Text)
            {
                //Debug.Log(Encoding.UTF8.GetString(resource));
                readTextResource(reader, targetObject);
            }
            else
                result = serializeFunc();
            ms.Close();
            Serializer.CloseStream();
            return result;
        }

        public static T CastInto<T>(object obj) // casting helper, actually referenced
        {
            return (T)obj;
        }

        static void readTextResource(BinaryReader reader, object targetObject, int indentLevel = 0)
        {
            // hoo boy this is gonna be a doozy
            char c = 'a';
            string textBuffer = "";
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                c = 'a'; // reset text
                // read line
                while (c != '\n' && reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string targetVar = "";
                    string targetValue = "";
                    c = reader.ReadChar();
                    int actualIndent = 0;
                    if (c == '\t')
                        actualIndent = 1;
                    while (c == '\t' && reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        c = reader.ReadChar();
                        if (c == '\t')
                            actualIndent++;
                    }
                   // Debug.LogFormat("Serializer: indent actual: {0}, indent target: {1}", actualIndent, indentLevel);
                    if (indentLevel > 0 && actualIndent < indentLevel)
                    {
                        // handle exiting from subtypes
                        reader.BaseStream.Position = reader.BaseStream.Position - indentLevel; // make sure you go back to beginning of line
                        //Debug.Log("Serializer: returning");
                        return;
                    }
                    textBuffer = "";
                    textBuffer += c;
                    while (c != '\t' && reader.BaseStream.Position < reader.BaseStream.Length) // read variable name
                    {
                        c = reader.ReadChar();
                        if (c != '\t')
                            textBuffer += c;
                    }
                    targetVar = textBuffer;
                    //Debug.LogFormat("Serializer: targetVar: {0}", targetVar);
                    textBuffer = "";
                    while (c != '\n' && reader.BaseStream.Position < reader.BaseStream.Length) // read variable value
                    {
                        c = reader.ReadChar();
                        if (c == '\r')
                            c = reader.ReadChar();
                        if (c != '\r' && c != '\n')
                            textBuffer += c;
                    }
                    targetValue = textBuffer;
                    //Debug.LogFormat("Serializer: targetValue: {0}", targetValue);

                    // now for the complicated bit, setting variables
                    //Debug.LogFormat("Serializer: Target object type: {0}", targetObject.GetType());
                    FieldInfo fieldInfo = targetObject.GetType().GetField(targetVar); // the simple part
                    if (fieldInfo != null)
                    {
                        Type fieldType = fieldInfo.FieldType;

                        // i'd do this in a switch/case if c# would actually let me but NOOO it has to be a constant :/
                        if (fieldType == typeof(string))
                        {
                            fieldInfo.SetValue(targetObject, targetValue.Trim('"'));
                        }
                        else if (fieldType == typeof(UTF8String))
                        {
                            fieldInfo.SetValue(targetObject, (UTF8String)targetValue.Trim('"'));
                        }
                        else if (fieldType == typeof(float))
                        {
                            fieldInfo.SetValue(targetObject, float.Parse(targetValue));
                        }
                        else if (fieldType == typeof(uint))
                        {
                            fieldInfo.SetValue(targetObject, uint.Parse(targetValue));
                        }
                        else if (fieldType == typeof(int))
                        {
                            fieldInfo.SetValue(targetObject, int.Parse(targetValue));
                        }
                        else if (fieldType == typeof(long))
                        {
                            fieldInfo.SetValue(targetObject, long.Parse(targetValue));
                        }
                        else if (fieldType == typeof(ulong))
                        {
                            fieldInfo.SetValue(targetObject, ulong.Parse(targetValue));
                        }
                        else if (fieldType == typeof(short))
                        {
                            fieldInfo.SetValue(targetObject, short.Parse(targetValue));
                        }
                        else if (fieldType == typeof(byte))
                        {
                            fieldInfo.SetValue(targetObject, byte.Parse(targetValue));
                        }
                        else if (fieldType == typeof(ushort))
                        {
                            fieldInfo.SetValue(targetObject, ushort.Parse(targetValue));
                        }
                        else if (fieldType == typeof(bool))
                        {
                            fieldInfo.SetValue(targetObject, bool.Parse(targetValue));
                        }
                        else if (fieldType.IsArray && fieldType.GetElementType().GetTypeInfo().IsClass)
                        {
                            Array array = Array.CreateInstance(fieldType.GetElementType(), int.Parse(targetValue));//new object[int.Parse(targetValue)];
                            int targetIndent = indentLevel + 1;
                            int arrayIndent = targetIndent;

                            textBuffer = "";

                            //Debug.LogFormat("Serializer: arrayIndent before: {0}", arrayIndent);

                            while (arrayIndent == targetIndent)
                            {
                                arrayIndent = 0;
                                c = '\t';
                                while (c == '\t' && reader.BaseStream.Position < reader.BaseStream.Length)
                                {
                                    c = reader.ReadChar();
                                    textBuffer += c;
                                    if (c == '\t')
                                        arrayIndent++;
                                }
                                if (arrayIndent < targetIndent)
                                    break;
                                // open bracket already read
                                if (c != '[')
                                {
                                    Debug.Log(textBuffer);
                                    Debug.LogErrorFormat("Serializer: Unexpected item in reading area: {0}", c);
                                }
                                c = reader.ReadChar(); // index
                                int index = int.Parse(c.ToString());
                                c = reader.ReadChar(); // close bracket
                                if (c != ']')
                                {
                                    Debug.LogErrorFormat("Serializer: Unexpected item in reading area: {0}", c);
                                }
                                c = reader.ReadChar(); // another tab
                                if (c != '\t')
                                {
                                    Debug.LogErrorFormat("Serializer: Unexpected item in reading area: {0}", c);
                                }
                                c = reader.ReadChar(); // new line
                                if (c == '\r')
                                {
                                    c = reader.ReadChar();
                                }
                                if (c != '\n')
                                {
                                    Debug.LogErrorFormat("Serializer: Unexpected item in reading area: {0}", c);
                                }
                                object targetSubobject = Activator.CreateInstance(fieldType.GetElementType());
                                //Debug.LogFormat("Serializer: Type of subobject: {0}", targetSubobject.GetType());
                                readTextResource(reader, targetSubobject, arrayIndent + 1);
                                //Debug.LogFormat("Serializer: arrayIndent after: {0}", arrayIndent);
                                array.SetValue(targetSubobject, index);
                            }
                            //Debug.Log(fieldType.ToString());
                            MethodInfo castIntoMethod = typeof(Serializer).GetMethod("CastInto").MakeGenericMethod(fieldType);

                            object o = array;
                            fieldInfo.SetValue(targetObject, castIntoMethod.Invoke(null, new[] { o }));
                        }
                        else
                        {
                            Debug.LogErrorFormat("Serializer: unknown type {0}!", fieldType.ToString());
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Serializer: the field {0} doesn't exist!", targetVar);
                    }
                }
            }
        }

        public static bool CheckVersion(int CurrentVersion)
        {
            SerializerInfo.DataVersion = CurrentVersion;
            Serialize(ref SerializerInfo.DataVersion); // store version
            return SerializerInfo.DataVersion <= CurrentVersion;
        }

        public static bool IsWriting()
        {
            return IsWriting(SerializerInfo);
        }

        public static bool IsWriting(serializer s)
        {
            if ((s.reader != null && s.writer != null) || (s.reader == null && s.writer == null))
            {
                Debug.LogError("Serializer: Invalid state! You must provide either a BinaryWriter or a BinaryReader, but not both!");
                return false;
            }
            if (s.reader != null)
            {
                return false;
            }
            else if (s.writer != null)
            {
                return true;
            }
            return false;
        }

        public static void SetBinaryWriter(BinaryWriter bw)
        {
            Serializer.SerializerInfo.writer = bw;
            Serializer.SerializerInfo.reader = null;
            Serializer.SerializerInfo.DataVersion = (int)Resource.Versions.LatestPlusOne - 1;
        }

        public static void SetBinaryReader(BinaryReader br)
        {
            Serializer.SerializerInfo.writer = null;
            Serializer.SerializerInfo.reader = br;
        }

        public static long GetBinaryReaderPosition()
        {
            return Serializer.SerializerInfo.reader.BaseStream.Position;
        }

        public static long GetBinaryReaderLength()
        {
            return Serializer.SerializerInfo.reader.BaseStream.Length;
        }

        public static Stream GetBaseStream()
        {
            if (IsWriting())
                return Serializer.SerializerInfo.writer.BaseStream;
            else
                return Serializer.SerializerInfo.reader.BaseStream;
        }

        public static void CloseStream()
        {
            if (Serializer.SerializerInfo.writer == null && Serializer.SerializerInfo.reader == null)
                return;
            if (IsWriting())
            {
                Serializer.SerializerInfo.writer.Close();
                Serializer.SerializerInfo.writer = null;
            } else
            {
                Serializer.SerializerInfo.reader.Close();
                Serializer.SerializerInfo.reader = null;
            }
        }

        // implement serialize function for every data type here

        // primitives

        public static void Serialize(ref char d, bool compress = true) // char
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                SerializerInfo.writer.Write(d);
            }
            else // reading
            {
                d = SerializerInfo.reader.ReadChar();
            }
        }

        public static void Serialize(ref sbyte d, bool compress = true) // int8
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            {
                d = SerializerInfo.reader.ReadSByte();
            }
        }

        static void write_ULEB128(long value)
        {
            ulong uvalue = (ulong)value & 0xFFFFFFFFFFFFFFFFL;
            if (value == long.MaxValue)
            {
                byte[] a = new byte[] { (byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF, 0x0F };
                Serialize(ref a, a.Length);
                return;
            }
            else if (value == 0)
            {
                byte a = 0;
                Serialize(ref a);
                return ;
            }
            while (true)
            {
                byte b = (byte)(value & 0x7fL);
                value >>= 7;
                if (value > 0L) b |= 128;
                Serialize(ref b);
                if (value == 0) break;
            }
        }

        public static long read_ULEB128()
        {
            long result = 0;
            int i = 0;
            while (true)
            {
                byte _byte = 0;
                Serialize(ref _byte);
                long b = (long)(_byte & 0xFFL);
                result |= (b & 0x7FL) << 7 * i;
                if ((b & 0x80L) == 0L)
                    break;
                i++;
            }
            return (long)((ulong)result >> 0);
        }

        public static void Serialize(ref short d, bool compress = true) // int16
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            {
                var data = SerializerInfo.reader.ReadBytes(2);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                d = BitConverter.ToInt16(data, 0);
            }
        }

        public static void Serialize(ref int d, bool compress = true, bool compressDouble = false) // int32
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
            {
                if (_IsWriting)
                {
                    if (compressDouble)
                    {
                        d *= 2;
                        uint d_conv = BitConverter.ToUInt32(BitConverter.GetBytes(d)); // jank to preserve negative portion
                        write_ULEB128(d_conv);
                    } else
                    {
                        write_ULEB128(d & 0xFFFFFFFFL);
                    }
                }
                else
                {
                    if (compressDouble)
                    {
                        //byte[] bytes = BitConverter.GetBytes((read_ULEB128()) / 2);
                        //d = BitConverter.ToInt32(bytes, 0); // jank to preserve negative portion
                        long v = read_ULEB128();
                        d = (int)((v >> 1 ^ -(v & 1)) & 0xFFFFFFFF);
                    }
                    else
                    {
                        d = (int)(read_ULEB128() & 0xFFFFFFFF);
                    }
                }
            }
            else
            {
                if (_IsWriting)
                {
                    var data = BitConverter.GetBytes(d);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    SerializerInfo.writer.Write(data);
                }
                else // reading
                {
                    var data = SerializerInfo.reader.ReadBytes(4);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    d = BitConverter.ToInt32(data, 0);
                }
            }
        }

        public static void Serialize(ref long d, bool compress = true) // int64
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
            {
                if (_IsWriting)
                    write_ULEB128(d);
                else
                    d = (read_ULEB128());
            }
            else
            {
                if (_IsWriting)
                {
                    var data = BitConverter.GetBytes(d);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    SerializerInfo.writer.Write(data);
                }
                else // reading
                {
                    var data = SerializerInfo.reader.ReadBytes(8);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    d = BitConverter.ToInt64(data, 0);
                }
            }
        }

        public static void Serialize(ref byte d, bool compress = true) // uint8
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                SerializerInfo.writer.Write(d);
            }
            else // reading
            {
                d = SerializerInfo.reader.ReadByte();
            }
        }


        public static void Serialize(ref ushort d, bool compress = true) // uint16
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            {
                var data = SerializerInfo.reader.ReadBytes(2);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                d = BitConverter.ToUInt16(data, 0);
            }
        }

        public static void Serialize(ref uint d, bool compress = true, bool compressDouble = false) // uint32
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
            {
                if (_IsWriting)
                {
                    if (compressDouble)
                        d *= 2;
                    write_ULEB128(d);
                }
                else
                {
                    d = (uint)read_ULEB128();
                    if (compressDouble)
                        d /= 2;
                }
            }
            else
            {
                if (_IsWriting)
                {
                    var data = BitConverter.GetBytes(d);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    SerializerInfo.writer.Write(data);
                }
                else // reading
                {
                    var data = SerializerInfo.reader.ReadBytes(4);
                    if (BitConverter.IsLittleEndian) // flip endianness
                        Array.Reverse(data);
                    d = BitConverter.ToUInt32(data, 0);
                }
            }
        }

        public static void Serialize(ref ulong d, bool compress = true) // uint64
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            {
                var data = SerializerInfo.reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                d = BitConverter.ToUInt64(data, 0);
            }
        }

        public static void Serialize(ref float d, bool compress = true) // float
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            { 
                var data = SerializerInfo.reader.ReadBytes(4);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                d = BitConverter.ToSingle(data, 0);
            }
        }

        public static void Serialize(ref double d, bool compress = true) // double
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                var data = BitConverter.GetBytes(d);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                SerializerInfo.writer.Write(data);
            }
            else // reading
            {
                var data = SerializerInfo.reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) // flip endianness
                    Array.Reverse(data);
                d = BitConverter.ToDouble(data, 0);
            }
        }

        public static void Serialize(ref bool d, bool compress = true) // boolean
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (_IsWriting)
            {
                SerializerInfo.writer.Write((byte)(d ? 0x1 : 0x0));
            }
            else // reading
            {
                d = SerializerInfo.reader.ReadByte() != 0x0;
            }
        }

        public static void Serialize(ref string d, bool compress = true) // string
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            int length = _IsWriting ? d.Length * 2 : 0;
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
                length *= 2;
            Serialize(ref length);
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
                length /= 2;
            var bytes = new byte[length];
            if (_IsWriting)
            {
                bytes = Encoding.BigEndianUnicode.GetBytes(d);
            }
            for (int i = 0; i < length; i++)
            {
                Serialize(ref bytes[i]);
            }
            if (!_IsWriting)
            {
                d = Encoding.BigEndianUnicode.GetString(bytes);
            }
            return;
        }

        public static void Serialize(ref UTF8String d, bool compress = true) // utf8 string
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            int length = _IsWriting ? d.Length : 0;
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
                length *= 2;
            Serialize(ref length);
            if (compress && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS) != 0)
                length /= 2;
            var bytes = new byte[length];
            if (_IsWriting)
            {
                bytes = d;
            }
            for (int i = 0; i < length; i++)
            {
                Serialize(ref bytes[i]);
            }
            if (!_IsWriting)
            {
                d = Encoding.UTF8.GetString(bytes);
            }
            return;
        }

        public static void Serialize(ref Vector2 d) // Vector2
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            Vector2 _d = _IsWriting ? d : new Vector2();
            Serialize(ref _d.x);
            Serialize(ref _d.y);
            d = _d;
        }

        public static void Serialize(ref Vector3 d) // Vector3
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            Vector3 _d = _IsWriting ? d : new Vector3();
            Serialize(ref _d.x);
            Serialize(ref _d.y);
            Serialize(ref _d.z);
            d = _d;
        }

        public static void Serialize(ref Vector4 d) // Vector4
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            Vector4 _d = _IsWriting ? d : new Vector4();
            Serialize(ref _d.x);
            Serialize(ref _d.y);
            Serialize(ref _d.z);
            Serialize(ref _d.w);
            d = _d;
        }

        public static void Serialize(ref Color d) // Color
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            Color _d = _IsWriting ? d : new Color();
            Serialize(ref _d.r);
            Serialize(ref _d.g);
            Serialize(ref _d.b);
            Serialize(ref _d.a);
            d = _d;
        }

        public static void Serialize(ref Matrix4x4 d) // Matrix4x4
        {
            float[] identity = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            float[] values = identity;
            bool _IsWriting = IsWriting(SerializerInfo);
            Matrix4x4 _d = _IsWriting ? d : new Matrix4x4();
            Vector4 Row1 = _d.GetColumn(0); // yes, column. LBP uses a different handedness
            Vector4 Row2 = _d.GetColumn(1);
            Vector4 Row3 = _d.GetColumn(2);
            Vector4 Row4 = _d.GetColumn(3);
            values = new float[] {
                Row1.x, Row1.y, Row1.z, Row1.w,
                Row2.x, Row2.y, Row2.z, Row2.w,
                Row3.x, Row3.y, Row3.z, Row3.w,
                Row4.x, Row4.y, Row4.z, Row4.w
            };
            int flags = 0xFFFF;

            if ((SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_MATRICES) != 0)
            {
                short s_flags = 0x7FFF;
                if (_IsWriting) {
                        flags = 0;
                    for (int i = 0; i < 16; ++i)
                        if (values[i] != identity[i])
                            flags |= (1 << i);
                    s_flags = (short)flags;
                }
                Serialize(ref s_flags);
                flags = (int)s_flags;
            }

            uint uflags = BitConverter.ToUInt32(BitConverter.GetBytes(flags));

            for (int i = 0; i < 16; ++i) {
                if (((uflags >> i) & 1) != 0)
                    Serialize(ref values[i]);
                else
                    values[i] = identity[i];
            }

            Row1 = new Vector4(values[0], values[1], values[2], values[3]);
            Row2 = new Vector4(values[4], values[5], values[6], values[7]);
            Row3 = new Vector4(values[8], values[9], values[10], values[11]);
            Row4 = new Vector4(values[12], values[13], values[14], values[15]);

            _d.SetColumn(0, Row1);
            _d.SetColumn(1, Row2);
            _d.SetColumn(2, Row3);
            _d.SetColumn(3, Row4);
            d = _d;
        }

        public static void Serialize(ref Quaternion d) // Quaternion
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            Quaternion _d = _IsWriting ? d : new Quaternion();
            Serialize(ref _d.x);
            Serialize(ref _d.y);
            Serialize(ref _d.z);
            Serialize(ref _d.w);
            d = _d;
        }

        public static void Serialize(ref Resource.Magic d) // resource magic
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            int length = 3;
            unsafe
            {
                byte* bytes = stackalloc byte[length];
                if (_IsWriting)
                {
                    Marshal.Copy(d, 0, (IntPtr)bytes, length);
                }
                for (int i = 0; i < length; i++)
                {
                    Serialize(ref bytes[i]);
                }
                if (!_IsWriting)
                {
                    Marshal.Copy((IntPtr)bytes, d, 0, length);
                }
            }
            return;
        }

        public static void Serialize(ref Resource.ResourceType d) // wrapper for resource magic in enum form
        {
            Resource.Magic temp = d;
            Serialize(ref temp);
            d = temp;
            return;
        }

        public static void Serialize(ref ResourceReference d) // wrapper for resource reference
        {
            d.Serialize();
            return;
        }

        public static void Serialize(ref Resource.SerializationMethod d) // serialization method
        {
            char temp1 = ' ';
            switch (d)
            {
                case Resource.SerializationMethod.Binary:
                    temp1 = 'b';
                    break;
                case Resource.SerializationMethod.BinaryEncrypted:
                    temp1 = 'e';
                    break;
                case Resource.SerializationMethod.Text:
                    temp1 = 't';
                    break;
                case Resource.SerializationMethod.Texture:
                    temp1 = ' ';
                    break;
            }

            Serialize(ref temp1);

            switch (temp1)
            {
                case 'b':
                    d = Resource.SerializationMethod.Binary;
                    break;
                case 'e':
                    d = Resource.SerializationMethod.BinaryEncrypted;
                    break;
                case 't':
                    d = Resource.SerializationMethod.Text;
                    break;
                case ' ':
                    d = Resource.SerializationMethod.Texture;
                    break;
                default:
                    d = Resource.SerializationMethod.NotSerialized;
                    break;
            }

            return;
        }

        // primitive arrays

        public static void Serialize(ref char[] d, bool compress = true) // char array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length);
            if (!IsWriting(SerializerInfo))
                d = new char[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref sbyte[] d, bool compress = true) // int8 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new sbyte[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref short[] d, bool compress = true) // int16 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new short[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref int[] d, bool compress = true, bool vector = false) // int32 array
        {
            //if (vector)
            //    Debug.Log("compressed vectors? " + SerializerInfo.compressionFlags + " " + (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_VECTORS));
            if (vector && (SerializerInfo.compressionFlags & COMPRESSION_FLAG.USE_COMPRESSED_VECTORS) != 0)
            {
                //Debug.Log("TABLE");
                table(ref d, compress);
                return;
            }
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new int[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void table(ref int[] d, bool compress = true) // table
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            if (IsWriting(SerializerInfo))
            {
                Serialize(ref length, compress);
                if (length == 0)
                    return;

                int overflow = d.Any(x => x > 0xFF) ? 2 : 1;

                Serialize(ref overflow);

                int[] indices = new int[d.Length];
                int[] overflowIndices = new int[d.Length];
                int loop = 0;

                for (int i = 0; i < d.Length; ++i)
                {
                    int value = d[i];
                    if (value - (loop * 0x100) >= 0x100) loop++;
                    indices[i] = value - (loop * 0x100);
                    if (overflow == 2)
                        overflowIndices[i] = loop;
                }

                for (int index = 0; index < indices.Length; index++)
                {
                    byte bIndex = (byte)indices[index];
                    Serialize(ref bIndex);
                }
                if (overflow == 2) {
                    for (int index = 0; index < overflowIndices.Length; index++)
                    {
                        byte bIndex = (byte)overflowIndices[index];
                        Serialize(ref bIndex);
                    }
                }   
                return;
            }
            else
            {
                //Debug.Log("TABLE READ");
                int indexCount = 0;
                Serialize(ref indexCount);
                if (indexCount == 0) { d = null; return; }
                int tableCount = 0;
                Serialize(ref tableCount);
                if (tableCount == 0) { d = null; return; }

                int[] indices = new int[indexCount];
                for (int i = 0; i < indexCount; ++i)
                {
                    byte bIndex = 0;
                    Serializer.Serialize(ref bIndex);
                    indices[i] = bIndex & 0xFF;
                }

                for (int i = 1; i < tableCount; ++i)
                {
                    for (int j = 0; j < indexCount; ++j)
                    {
                        byte bIndex = 0;
                        Serializer.Serialize(ref bIndex);
                        indices[j] += (bIndex & 0xFF) * 0x100;
                    }
                }

            }

            return;
        }

        public static void Serialize(ref long[] d, bool compress = true) // int64 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new long[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref byte[] d, bool compress = true) // uint8 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new byte[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref byte[] d, int length) // raw byte array, only use if you know what you're doing!
        {
            length = IsWriting(SerializerInfo) ? 0 : length;
            //Serialize(ref length);
            if (!IsWriting(SerializerInfo))
                d = new byte[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref ushort[] d, bool compress = true) // uint16 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new ushort[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref uint[] d, bool compress = true) // uint32 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new uint[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref ulong[] d, bool compress = true) // uint64 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new ulong[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i], compress);
            }
            return;
        }

        public static void Serialize(ref float[] d, bool compress = true) // float array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new float[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref double[] d, bool compress = true) // double array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new double[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref bool[] d, bool compress = true) // boolean array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new bool[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref Vector3[] d, bool compress = true) // Vector3 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new Vector3[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref Vector4[] d, bool compress = true) // Vector4 array
        {
            int length = IsWriting(SerializerInfo) ? d.Length : 0;
            Serialize(ref length, compress);
            if (!IsWriting(SerializerInfo))
                d = new Vector4[length];
            for (int i = 0; i < length; i++)
            {
                Serialize(ref d[i]);
            }
            return;
        }

        // certain classes

        public static void Serialize(ref SerializableMonoBehaviour d) // Serializable MonoBehaviour, write only
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            if (!_IsWriting) {
                Debug.LogError("This method is write only!");
                return;
            }
            UTF8String type = d.GetType().FullName;
            Serialize(ref type);
            d.Serialize();
            return;
        }

        public static void Serialize(ref Rigidbody d) // rigidbody
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            float angularDrag = d != null ? d.angularDamping : 0;
            Serialize(ref angularDrag);
            if (d != null)
                d.angularDamping = angularDrag;

            Vector3 angularVelocity = d != null ? d.angularVelocity : Vector3.zero;
            Serialize(ref angularVelocity);
            if (d != null)
                d.angularVelocity = angularVelocity;

            int collisionDetectionMode = d != null ? (int)d.collisionDetectionMode : 0;
            Serialize(ref collisionDetectionMode);
            if (d != null)
                d.collisionDetectionMode = (CollisionDetectionMode)collisionDetectionMode;

            int constraints = d != null ? (int)(RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY) : 0;
            Serialize(ref constraints);
            if (d != null)
                d.constraints = (RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY);//(RigidbodyConstraints)constraints;

            bool detectCollisions = d != null ? d.detectCollisions : false;
            Serialize(ref detectCollisions);
            if (d != null)
                d.detectCollisions = detectCollisions;

            float drag = d != null ? d.linearDamping : 0;
            Serialize(ref drag);
            if (d != null)
                d.linearDamping = drag;

            bool freezeRotation = d != null ? d.freezeRotation : false;
            Serialize(ref freezeRotation);
            if (d != null)
                d.freezeRotation = freezeRotation;

            Vector3 inertiaTensor = d != null ? d.inertiaTensor : Vector3.zero;
            Serialize(ref inertiaTensor);
            if (d != null)
                d.inertiaTensor = inertiaTensor;

            Quaternion inertiaTensorRotation = d != null ? d.inertiaTensorRotation : Quaternion.identity;
            Serialize(ref inertiaTensorRotation);
            if (d != null)
                d.inertiaTensorRotation = inertiaTensorRotation;

            int interpolation = d != null ? (int)d.interpolation : 0;
            Serialize(ref interpolation);
            if (d != null)
                d.interpolation = d != null ? (RigidbodyInterpolation)interpolation : 0;

            bool isKinematic = d != null ? d.isKinematic : false;
            Serialize(ref isKinematic);
            if (d != null)
                d.isKinematic = isKinematic;

            float mass = d != null ? d.mass : 0;
            Serialize(ref mass);
            if (d != null)
                d.mass = mass;

            float maxAngularVelocity = d != null ? d.maxAngularVelocity : 0;
            Serialize(ref maxAngularVelocity);
            if (d != null)
                d.maxAngularVelocity = maxAngularVelocity;
            
            float maxDepenetrationVelocity = d != null ? d.maxDepenetrationVelocity : 0;
            Serialize(ref maxDepenetrationVelocity);
            if (d != null)
                d.maxDepenetrationVelocity = maxDepenetrationVelocity;

            Vector3 position = d != null ? d.position : Vector3.zero;
            Serialize(ref position);
            if (d != null)
                d.position = position;

            Quaternion rotation = d != null ? d.rotation : Quaternion.identity;
            Serialize(ref rotation);
            if (d != null)
                d.rotation = rotation;

            float sleepThreshold = d != null ? d.sleepThreshold : 0;
            Serialize(ref sleepThreshold);
            if (d != null)
                d.sleepThreshold = sleepThreshold;

            int solverIterations = d != null ? d.solverIterations : 0;
            Serialize(ref solverIterations);
            if (d != null)
                d.solverIterations = solverIterations;

            int solverVelocityIterations = d != null ? d.solverVelocityIterations : 0;
            Serialize(ref solverVelocityIterations);
            if (d != null)
                d.solverVelocityIterations = solverVelocityIterations;

            bool useGravity = d != null ? d.useGravity : false;
            Serialize(ref useGravity);
            if (d != null)
                d.useGravity = useGravity;

            Vector3 velocity = d != null ? d.linearVelocity : Vector3.zero;
            Serialize(ref velocity);
            if (d != null)
                d.linearVelocity = velocity;

            return;
        }

        enum ColliderTypes
        {
            Box,
            Sphere,
            Capsule,
            Mesh
        }

        public static void Serialize(ref BoxCollider d) // collider
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            bool isTrigger = d.isTrigger;
            Serialize(ref isTrigger);
            d.isTrigger = isTrigger;

            Vector3 center = d.center;
            Serialize(ref center);
            d.center = center;

            Vector3 size = d.size;
            Serialize(ref size);
            d.size = size;
 
            return;
        }

        public static void Serialize(ref SphereCollider d) // collider
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            bool isTrigger = d.isTrigger;
            Serialize(ref isTrigger);
            d.isTrigger = isTrigger;

            Vector3 center = d.center;
            Serialize(ref center);
            d.center = center;

            float radius = d.radius;
            Serialize(ref radius);
            d.radius = radius;

            return;
        }

        public static void Serialize(ref CapsuleCollider d) // collider
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            bool isTrigger = d.isTrigger;
            Serialize(ref isTrigger);
            d.isTrigger = isTrigger;

            Vector3 center = d.center;
            Serialize(ref center);
            d.center = center;

            float radius = d.radius;
            Serialize(ref radius);
            d.radius = radius;

            float height = d.height;
            Serialize(ref height);
            d.height = height;

            int direction = d.direction;
            Serialize(ref direction);
            d.direction = direction;

            return;
        }

        public static void Serialize(ref MeshCollider d) // collider
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            

            return;
        }

        public static void Serialize(ref GameObject d) // GameObject, or as much as can be serialized from it.
        {
            bool _IsWriting = IsWriting(SerializerInfo);

            var activeSelf = d.activeSelf;
            Serialize(ref activeSelf);
            if (d.activeSelf != activeSelf) // precaution to not call any functions in MonoBehaviour if it hasn't changed states
                d.SetActive(activeSelf);

            var name = d.name;
            Serialize(ref name);
            d.name = name;

            var layer = d.layer;
            Serialize(ref layer);
            d.layer = layer;

            UTF8String tag = d.tag;
            Serialize(ref tag);
            d.tag = tag;

            Transform _transform = d.transform;
            Serialize(ref _transform);
            d.transform.localPosition = _transform.localPosition;
            d.transform.localRotation = _transform.localRotation;
            d.transform.localScale = _transform.localScale;

            { // rigidbody

                Rigidbody Rigidbody = d.GetComponent<Rigidbody>();

                bool hasRigidbody = Rigidbody;

                Serialize(ref hasRigidbody);

                if (hasRigidbody && !d.GetComponent<Rigidbody>())
                {
                    Rigidbody = d.AddComponent<Rigidbody>();
                }

                if (hasRigidbody)
                {
                    Serialize(ref Rigidbody);
                }

            }

            { // collider

                Collider Collider = d.GetComponent<Collider>();

                bool hasCollider = Collider != null;

                Serialize(ref hasCollider);

                if (hasCollider)
                {

                    string stype = d.GetType().Name;

                    ColliderTypes type = ColliderTypes.Box;

                    switch (stype)
                    {
                        case "BoxCollider":
                            type = ColliderTypes.Box;
                            break;
                        case "SphereCollider":
                            type = ColliderTypes.Sphere;
                            break;
                        case "CapsuleCollider":
                            type = ColliderTypes.Capsule;
                            break;
                        case "MeshCollider":
                            type = ColliderTypes.Mesh;
                            break;
                    }

                    byte itype = (byte)type;
                    Serialize(ref itype);
                    type = (ColliderTypes)itype;

                    if (_IsWriting)
                    {
                        switch (type)
                        {
                            case ColliderTypes.Box:
                                BoxCollider boxCollider = d.GetComponent<BoxCollider>();
                                Serialize(ref boxCollider);
                                break;
                            case ColliderTypes.Sphere:
                                SphereCollider sphereCollider = d.GetComponent<SphereCollider>();
                                Serialize(ref sphereCollider);
                                break;
                            case ColliderTypes.Capsule:
                                CapsuleCollider capsuleCollider = d.GetComponent<CapsuleCollider>();
                                Serialize(ref capsuleCollider);
                                break;
                            case ColliderTypes.Mesh:
                                MeshCollider meshCollider = d.GetComponent<MeshCollider>();
                                Serialize(ref meshCollider);
                                break;
                        }
                    }
                    else
                    {
                        switch (type)
                        {
                            case ColliderTypes.Box:
                                BoxCollider boxCollider = d.AddComponent<BoxCollider>();
                                Serialize(ref boxCollider);
                                break;
                            case ColliderTypes.Sphere:
                                SphereCollider sphereCollider = d.AddComponent<SphereCollider>();
                                Serialize(ref sphereCollider);
                                break;
                            case ColliderTypes.Capsule:
                                CapsuleCollider capsuleCollider = d.AddComponent<CapsuleCollider>();
                                Serialize(ref capsuleCollider);
                                break;
                            case ColliderTypes.Mesh:
                                MeshCollider meshCollider = d.AddComponent<MeshCollider>();
                                Serialize(ref meshCollider);
                                break;
                        }
                    }
                }
            }

            // do monobehaviours now
            {
                SerializableMonoBehaviour[] _smb = d.GetComponents<SerializableMonoBehaviour>();
                int smbLength = _smb.Length;

                Serialize(ref smbLength);

                SerializableMonoBehaviour[] smb = _IsWriting ? _smb : new SerializableMonoBehaviour[smbLength];

                if (_IsWriting)
                {
                    for (int i = 0; i < smb.Length; i++)
                    {
                        Serialize(ref smb[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < smb.Length; i++)
                    {
                        UTF8String type = "SerializableMonoBehaviour";
                        Serialize(ref type);
                        SerializableMonoBehaviour script = (SerializableMonoBehaviour)d.AddComponent(Type.GetType(type));
                        //Debug.Log(type + " " + Type.GetType(type));
                        try
                        {
                            script.Serialize();
                        }
                        catch { }
                    }
                }
            }

            { // child gameobjects
                int objectCount = d.transform.childCount;
                Serialize(ref objectCount);
                GameObject[] objects = new GameObject[objectCount];
                
                if (_IsWriting) {
                    int i = 0;
                    foreach (Transform trans in d.transform)
                    {
                        objects[i] = trans.gameObject;
                        i++;
                    }
                }
                for (int i=0; i<objects.Length; i++)
                {
                    if (!_IsWriting) {
                        objects[i] = new GameObject();
                        objects[i].transform.SetParent(d.transform);
                    }
                    Serialize(ref objects[i]);
                }
            }

            return;
        }

        public static void Serialize(ref RMusicSetting.StereoPair d) // StereoPair
        {
            d.Serialize();
            return;
        }

        public static void Serialize(ref RMusicSetting.StereoPair[] d) // StereoPair array
        {
            bool _IsWriting = IsWriting(SerializerInfo);
            int length = _IsWriting ? d.Length : 0;
            Serialize(ref length);
            if (!IsWriting(SerializerInfo))
                d = new RMusicSetting.StereoPair[length];
            for (int i = 0; i < length; i++)
            {
                if (!_IsWriting)
                    d[i] = new RMusicSetting.StereoPair();
                Serialize(ref d[i]);
            }
            return;
        }

        public static void Serialize(ref Transform d) // Transform's important info
        {
            Vector3 _localPosition = d.localPosition;
            Quaternion _localRotation = d.localRotation;
            Vector3 _localScale = d.localScale;

            Serialize(ref _localPosition);
            Serialize(ref _localRotation);
            Serialize(ref _localScale);

            d.localPosition = _localPosition;
            d.localRotation = _localRotation;
            d.localScale = _localScale;
        }
    }
}