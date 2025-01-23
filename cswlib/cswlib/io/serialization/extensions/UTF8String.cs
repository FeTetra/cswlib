using System.Text;

namespace cswlib.cswlib.io.serialization.extensions
{
    public class UTF8String
    {
        public string String;
        UTF8String(string s)
        {
            String = s; //chars = Encoding.UTF8.GetBytes(s);
        }

        UTF8String(byte[] b)
        {
            String = Encoding.UTF8.GetString(b); //chars = b;
        }

        public int Length
        {
            get { return String.Length; } // get method
        }

        public string propertyString
        {
            get
            {
                if (String == null)
                    return "";
                return String; // Encoding.UTF8.GetString(chars);
            }
            set
            {
                String = value; // Encoding.UTF8.GetBytes(value);
            }
        }

        public static implicit operator byte[](UTF8String u) => Encoding.UTF8.GetBytes(u.String);
        public static implicit operator char[](UTF8String u) => u.String.ToCharArray();
        public static implicit operator string(UTF8String u) => u.String;

        public static implicit operator UTF8String(byte[] b) => new UTF8String(b);
        public static implicit operator UTF8String(char[] c) => Encoding.UTF8.GetBytes(c);
        public static implicit operator UTF8String(string s) => new UTF8String(s);
        public override string ToString() => String;
    }
}