using System.Text;
using cswlib.cswlib.io.serialization;
using cswlib.cswlib.io.serialization.types;
using cswlib.cswlib.io.serialization.extensions;
using cswlib.cswlib.util;

// Credit to torutheredfox for these resource definitions
namespace cswlib.cswlib.resource 
{
    public class Resource
    {
        public class Magic
        {
            public UTF8String magic;
            private Magic(UTF8String magic)
            {
                this.magic = magic;
            }

            // from
            public static implicit operator UTF8String(Magic m) => m.magic;
            public static implicit operator string(Magic m) => m.magic;
            public static implicit operator byte[](Magic m) => Encoding.ASCII.GetBytes(m); // make sure it's ASCII

            // to
            public static implicit operator Magic(byte[] b) => new Magic(Encoding.ASCII.GetString(b));
            public static implicit operator Magic(string s) => new Magic(s);
            public static implicit operator Magic(UTF8String s) => new Magic(s);

            public static implicit operator Magic(ResourceType r)
            {
                UTF8String magicString = "UNK";

                switch (r)
                {
                    case ResourceType.Texture:
                        magicString = "TEX";
                        break;
                    case ResourceType.Level:
                        magicString = "LVL";
                        break;
                    case ResourceType.Mesh:
                        magicString = "MSH";
                        break;
                    case ResourceType.MusicSetting:
                        magicString = "MUS";
                        break;
                    case ResourceType.GfxMaterial:
                        magicString = "GMT";
                        break;
                    case ResourceType.Animation:
                        magicString = "ANM";
                        break;
                };

                return magicString;
            }

            public static implicit operator ResourceType(Magic m)
            {
                ResourceType resourceType = ResourceType.Unknown;

                switch (m)
                {
                    case "TEX":
                        resourceType = ResourceType.Texture;
                        break;
                    case "GTF":
                        resourceType = ResourceType.GcmTexture;
                        break;
                    case "LVL":
                        resourceType = ResourceType.Level;
                        break;
                    case "MSH":
                        resourceType = ResourceType.Mesh;
                        break;
                    case "MUS":
                        resourceType = ResourceType.MusicSetting;
                        break;
                    case "GMT":
                        resourceType = ResourceType.GfxMaterial;
                        break;
                    case "ANM":
                        resourceType = ResourceType.Animation;
                        break;
                };

                return resourceType;
            }
        }
        
        public enum ResourceType
        {
            Unknown,
            Texture = 1,
            GcmTexture = 1,
            Mesh,
            PixelShader,
            VertexShader,
            Animation,
            GuidSubstitution,
            GfxMaterial,
            SpuElf,
            Level,
            FileName,
            Script,
            SettingsCharacter,
            FileOfBytes,
            SettingsSoftPhys,
            FontFace,
            Material,
            DownloadableContent,
            EditorSettings,
            Joint,
            GameConstants,
            PoppetSettings,
            CachedLevelData,
            SyncedProfile,
            Bevel,
            Game,
            SettingsNetwork,
            Packs,
            BigProfile,
            SlotList,
            Translation,
            ParticleSettings,
            LocalProfile,
            LimitsSettings,
            ParticleTemplate,
            ParticleLibrary,
            AudioMaterials,
            SettingsFluid,
            Plan,
            TextureList,
            MusicSetting,
            MixerSettings,
            ReplayConfig,
            Palette,

            Last
        }

        public enum SerializationMethod
        {
            NotSerialized,
            Binary,
            Text,
            Texture,
            BinaryEncrypted
        }

        public enum Versions
        {
            ResourceInitial = 1,
            add_dependencies = 0x109,
            add_zlib_compression_flag = 0x189,
            RMusicSetting_add_copyright_field = 0x1ed,
            lbp1_pod_mesh = 0x239,
            MeshLoader_ChangePathToResourceRef,
            lbp1_crown_mesh = 0x23B,
            lbp1_spaceman_mesh = 0x23C,
            add_branch_info = 0x271,
            add_compression_flags_other_branch = 0x272,
            add_compression_flags = 0x297,
            RMusicSetting_add_sting_field = 0x390, // pure guessium
            lbp2_latest = 0x3F8,
            LatestPlusOne
            // currently unknown, research
            //RMusicSetting_add_sting_field = 0xFFFF
        }

        ResourceType resourceType = ResourceType.Unknown;
        public SerializationMethod serializationMethod = SerializationMethod.Binary;

        public bool serializeAsText = false;

        byte[] serializedData;

        public System.Object resourceObject;

        ResourceReference[] dependencies = new ResourceReference[0];

        long offset = 0;
        long length = 0;

        public Resource(ResourceType resourceType, SerializationMethod serializationMethod, byte[] data)
        {
            this.resourceType = resourceType;
            this.serializationMethod = serializationMethod;
            serializedData = data;
            this.Serialize();
        }

        public Resource(ResourceType t)
        {
            resourceType = t;
            this.Serialize();
        }

        public Resource(ResourceType t, long offset, long length)
        {
            resourceType = t;
            this.offset = offset;
            this.length = length;
            this.Serialize();
        }

        // probably won't do that here
        //public void Load(int guid)
        //{
        //    // TODO: implement resource loading by guid
        //}
        //public void Load(string path)
        //{
        //    // TODO: implement resource loading by path
        //}
        //public void Load(byte[] hash)
        //{
        //    // TODO: implement resource loading by hash
        //}

        readonly byte fullCompression = (byte)(Serializer.COMPRESSION_FLAG.USE_COMPRESSED_INTEGERS |
                                        Serializer.COMPRESSION_FLAG.USE_COMPRESSED_VECTORS |
                                        Serializer.COMPRESSION_FLAG.USE_COMPRESSED_MATRICES);

        public Serializer.SERIALIZER_RESULT Serialize()
        {
            // local things for assisting with serialization
            bool _isWriting = Serializer.IsWriting();

            byte compressionFlags = Serializer.SerializerInfo.compressionFlags;
            Serializer.SerializerInfo.compressionFlags = 0;

            int dependencyTableOffset = -1;
            bool isCompressed = true;

            // seralize data
            ResourceType _resourceType = resourceType;

            System.IO.Stream BaseStream = Serializer.GetBaseStream();
            BaseStream.Position = offset;

            Serializer.Serialize(ref _resourceType);
            Serializer.Serialize(ref serializationMethod);

            // handle non-binary resources
            if (serializationMethod == SerializationMethod.Text)
            {
                char lineBreak = '\n';
                Serializer.Serialize(ref lineBreak);
                if (lineBreak == '\r')
                {
                    Serializer.Serialize(ref lineBreak); // do it again lol
                }
                if (lineBreak != '\n')
                {
                    BaseStream.Position = BaseStream.Position - 1;
                }
            }

            if (serializationMethod == SerializationMethod.Texture || serializationMethod == SerializationMethod.Text)
            {
                Serializer.Serialize(ref serializedData, (int)(length - (Serializer.GetBinaryReaderPosition()-offset)));
                return Serializer.SERIALIZER_RESULT.OK;
            }

            // error handling
            if (!_isWriting && _resourceType != resourceType)
            {
                Util.err_printf("Resource: Unexpected resource type %s! Expected %s\n", _resourceType.ToString(), resourceType.ToString());
                return Serializer.SERIALIZER_RESULT.INVALID;
            }

            if (!Serializer.CheckVersion((int)Versions.LatestPlusOne - 1))
            {
                Util.err_printf("File too new!\n");
                return Serializer.SERIALIZER_RESULT.FORMAT_TOO_NEW;
            }

            // continue reading regular binary resource
            bool hasDependencies = Serializer.SerializerInfo.DataVersion >= (int)Versions.add_dependencies;
            if (hasDependencies)
            {
                Serializer.Serialize(ref dependencyTableOffset, compress: false);
            }

            if (Serializer.SerializerInfo.DataVersion >= (int)Versions.add_branch_info)
            {
                Serializer.Serialize(ref Serializer.SerializerInfo.branchID, compress: false);
                Serializer.Serialize(ref Serializer.SerializerInfo.branchRevision, compress: false);
                //Debug.Log("branchID: " + Serializer.SerializerInfo.branchID + " \nbranchRevision: " + Serializer.SerializerInfo.branchRevision);
            }

            if (Serializer.SerializerInfo.DataVersion >= (int)Versions.add_compression_flags || (Serializer.SerializerInfo.DataVersion == (int)Versions.add_compression_flags_other_branch && Serializer.SerializerInfo.branchID != 0))
            {
                //Debug.Log("Compression flags!");
                Serializer.Serialize(ref compressionFlags, compress: false);
                //if (Serializer.SerializerInfo.compressionFlags != 0)
                //    return Serializer.SERIALIZER_RESULT.NOT_IMPLEMENTED;
            } else
            {
                compressionFlags = 0;
            }

            if (Serializer.SerializerInfo.DataVersion >= (int)Versions.add_zlib_compression_flag)
            {
                 Serializer.Serialize(ref isCompressed, compress: false);
            }

            if (isCompressed)
            {
                if (_isWriting)
                    new CompressedData(serializedData); 
                else
                    serializedData = new CompressedData();
            } 
            else
            {
                if (_isWriting)
                {
                    Serializer.Serialize(ref serializedData, 0);
                }
                else
                {
                    if (dependencyTableOffset != -1)
                        Serializer.Serialize(ref serializedData, (int)(dependencyTableOffset - Serializer.GetBinaryReaderPosition()));
                    else
                        Serializer.Serialize(ref serializedData, (int)(Serializer.GetBinaryReaderLength() - Serializer.GetBinaryReaderPosition()));
                }
            }

            if (!hasDependencies) return Serializer.SERIALIZER_RESULT.OK;

            if (_isWriting)
            {
                dependencyTableOffset = (int) (BaseStream.Position - offset);
                BaseStream.Position = 8 + offset;
                Serializer.Serialize(ref dependencyTableOffset, compress: false);
                BaseStream.Position = dependencyTableOffset + offset;
            }

            int len = dependencies != null ? dependencies.Length : 0;
            Serializer.Serialize(ref len, compress: false);
            if (!_isWriting) dependencies = new ResourceReference[len];

            for (int i = 0; i < len; ++i)
            {
                if (!_isWriting) 
                    dependencies[i] = new ResourceReference();
                dependencies[i].Serialize(true, false);
            }

            Serializer.SerializerInfo.compressionFlags = compressionFlags;
            //Debug.Log(compressionFlags);

            return Serializer.SERIALIZER_RESULT.OK;
        }

        public static implicit operator byte[](Resource r) => r.serializedData;
    }

    public static class DictionaryExtension
    {
        public static Dictionary<TValue, TKey> Reverse<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            var dictionary = new Dictionary<TValue, TKey>();
            foreach (var entry in source)
            {
                if (!dictionary.ContainsKey(entry.Value))
                    dictionary.Add(entry.Value, entry.Key);
            }
            return dictionary;
        }
    }
}