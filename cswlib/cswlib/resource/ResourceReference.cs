using System.Text;
using cswlib.cswlib.io.serialization;
using cswlib.cswlib.io.serialization.extensions;

namespace cswlib.cswlib.resource 
{
    /*[CustomPropertyDrawer(typeof(ResourceReference))]
    public class ResourceReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            //var itemProp = property.FindPropertyRelative("stringAssigner");
            //if (itemProp.objectReferenceValue is GameObject go && go != null)
            SerializedObject Obj = property.serializedObject;
            label = new GUIContent("Resource");
            Rect pos = EditorGUI.PrefixLabel(position, label);
            pos.width *= 0.5f;
            Rect p1 = pos, p2 = pos;
            p2.x += pos.width;
            //EditorGUI.PropertyField(p1, itemProp, GUIContent.none);

            Debug.Log("Grr "+(string)Obj.targetObject.GetType().GetProperty("stringAssigner").GetValue(Obj.targetObject));

            GUIContent animalType = new GUIContent((string)Obj.targetObject.GetType().GetProperty("stringAssigner").GetValue(Obj.targetObject));

            SerializedProperty nameProperty = Obj.FindProperty("Path");
            EditorGUI.PropertyField(p1, nameProperty, animalType, true);
        }
    }*/
    
    public class ResourceReference
    {
        public enum ResourceReferenceType
        {
            Hash,
            GUID,
            Path
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
        
        public string stringAssigner
        {
            get
            {
                switch(referenceType)
                {
                    case ResourceReferenceType.Hash:
                        StringBuilder a = new StringBuilder();

                        for (int i=0; i<Hash.Length; i++)
                        {
                            a.Append(Hash[i].ToString("x2"));
                        }

                        return string.Format("h{0}", a.ToString());
                    case ResourceReferenceType.Path:
                        return Path;
                    case ResourceReferenceType.GUID:
                        return string.Format("g{0}", GUID);
                }
                return string.Format("g{0}", GUID);
            }
            set
            {
                if (value.StartsWith("h"))
                {
                    referenceType = ResourceReferenceType.Hash;
                    Hash = StringToByteArray(value.Substring(1));
                }
                else if (value.StartsWith("g"))
                {
                    referenceType = ResourceReferenceType.GUID;
                    GUID = UInt32.Parse(value.Substring(1));
                } else
                {
                    referenceType = ResourceReferenceType.Path;
                    Path = value;
                }
            }
        }
        
        public ResourceReferenceType referenceType = ResourceReferenceType.GUID;
        public UTF8String Path;
        public byte[] Hash = new byte[0x14];
        public uint GUID = 0;

        Resource resource;

        public Serializer.SERIALIZER_RESULT Serialize(bool IncludeTypes = false, bool ClassPointer = true)
        {
            int flags = 0;

            // Technically flags are serialized as part of CResource, not the CResourceDescriptor, but
            // I don't think there'll be any real issues here.
            if (Serializer.SerializerInfo.DataVersion > 0x22e && ClassPointer) Serializer.Serialize(ref flags);

            byte _referenceType = (byte)(1 << (int)referenceType);
            Serializer.Serialize(ref _referenceType);
            referenceType = (ResourceReferenceType)(_referenceType >> 1);


            ResourceReferenceType guidFlag = ResourceReferenceType.GUID;
            ResourceReferenceType hashFlag = ResourceReferenceType.Hash;

            // Older revisions had the flags for hash and guid flipped, but specifically
            // only for class pointers, not in descriptors.
            if (Serializer.SerializerInfo.DataVersion < 0x18B && ClassPointer)
            {
                guidFlag = ResourceReferenceType.Hash;
                hashFlag = ResourceReferenceType.GUID;
            }

            if ((referenceType & guidFlag) != 0)
                Serializer.Serialize(ref GUID);
            if ((referenceType & hashFlag) != 0)
                Serializer.Serialize(ref Hash, 0x14);
            // Should paths be converted to local GUIDs?
            if ((referenceType & ResourceReferenceType.Path) != 0)
                Serializer.Serialize(ref Path);

            if (IncludeTypes == true)
            {
                int _Type = 0;
                Serializer.Serialize(ref _Type);
            }

            return Serializer.SERIALIZER_RESULT.OK;
        }

        public static implicit operator Resource(ResourceReference r) => r.resource;
        public static implicit operator uint(ResourceReference r) => r.GUID;
    }
}