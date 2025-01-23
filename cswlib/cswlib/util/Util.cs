using System.Runtime.InteropServices;
using size_t = System.Nullable<uint>;

// TODO: Remove dependency on Unity

namespace cswlib.cswlib.util
{
    using out_fct_type = System.Action<char, System.IO.Stream, size_t>;

    public class Util
    {
        static void _out_null(char character, Stream buffer, size_t idx)
        {
            return;
        }

        static void _out_buffer(char character, Stream buffer, size_t idx)
        {
            if (character != 0)
            {
                buffer.WriteByte((byte)character); //buffer.Write(BitConverter.GetBytes(character));
            }
        }

        static readonly uint FLAGS_ZEROPAD    = (1U <<  0);
        static readonly uint FLAGS_LEFT       = (1U <<  1);
        static readonly uint FLAGS_PLUS       = (1U <<  2);
        static readonly uint FLAGS_SPACE      = (1U <<  3);
        static readonly uint FLAGS_HASH       = (1U <<  4);
        static readonly uint FLAGS_UPPERCASE  = (1U <<  5);
        static readonly uint FLAGS_CHAR       = (1U <<  6);
        static readonly uint FLAGS_SHORT      = (1U <<  7);
        static readonly uint FLAGS_LONG       = (1U <<  8);
        static readonly uint FLAGS_LONG_LONG  = (1U <<  9);
        static readonly uint FLAGS_PRECISION  = (1U << 10);
        static readonly uint FLAGS_ADAPT_EXP  = (1U << 11);

        // internal ASCII string to unsigned int conversion
        static uint _atoi(ref string str, ref uint formatPos)
        {
            uint i = 0U;
            while (char.IsDigit(str[(int)formatPos])) {
                i = i * 10U + (uint)((str[(int)formatPos++]) - '0');
            }
            return i;
        }

        // output the specified string in reverse, taking care of any zero-padding
        static size_t _out_rev(out_fct_type _out, Stream buffer, size_t idx, char[] buf, size_t len, uint width, uint flags)
        {
            size_t start_idx = idx;

            // pad spaces up to given width
            if ((flags & FLAGS_LEFT) == 0 && (flags & FLAGS_ZEROPAD) == 0)
            {
                for (size_t i = len; i < width; i++)
                {
                    _out(' ', buffer, idx++);
                }
            }

            // reverse string
            while (len != 0)
            {
                _out(buf[(int)--len], buffer, idx++);
            }

            // append pad spaces up to given width
            if ((flags & FLAGS_LEFT) != 0)
            {
                while (idx - start_idx < width)
                {
                    _out(' ', buffer, idx++);
                }
            }

            return idx;
        }

        // internal _putchar wrapper
        static void _out_char(char character, Stream buffer, size_t idx)
        {
            //(void)buffer; (void)idx; (void)maxlen;
            if (character != 0)
            {
                buffer.WriteByte((byte)character);//buffer.Write(BitConverter.GetBytes(character));//Debug.Log(character);
            } else
            {
                buffer.Position = 0;
                StreamReader reader = new StreamReader(buffer);
                //Debug.Log(reader.ReadToEnd());
            }
        }

        // internal _putchar wrapper
        static void _out_char_wrn(char character, Stream buffer, size_t idx)
        {
            //(void)buffer; (void)idx; (void)maxlen;
            if (character != 0)
            {
                buffer.WriteByte((byte)character);//buffer.Write(BitConverter.GetBytes(character));//Debug.Log(character);
            }
            else
            {
                buffer.Position = 0;
                StreamReader reader = new StreamReader(buffer);
                //Debug.LogWarning(reader.ReadToEnd());
            }
        }

        // internal _putchar wrapper
        static void _out_char_err(char character, Stream buffer, size_t idx)
        {
            //(void)buffer; (void)idx; (void)maxlen;
            if (character != 0)
            {
                buffer.WriteByte((byte)character);// buffer.Write(BitConverter.GetBytes(character));//Debug.Log(character);
            }
            else
            {
                buffer.Position = 0;
                StreamReader reader = new StreamReader(buffer);
                //Debug.LogError(reader.ReadToEnd());
            }
        }

        // internal itoa format
        static size_t _ntoa_format(out_fct_type _out, Stream buffer, size_t idx, char[] buf, size_t len, bool negative, uint _base, uint prec, uint width, uint flags)
        {
            // pad leading zeros
            if ((flags & FLAGS_LEFT) == 0)
            {
                if (width != 0 && (flags & FLAGS_ZEROPAD) != 0 && (negative || ((flags & (FLAGS_PLUS | FLAGS_SPACE)) != 0)))
                {
                    width--;
                }
                while ((len < prec) && (len < 32))
                {
                    buf[(int)len++] = '0';
                }
                while ((flags & FLAGS_ZEROPAD) != 0 && (len < width) && (len < 32))
                {
                    buf[(int)len++] = '0';
                }
            }

            // handle hash
            if ((flags & FLAGS_HASH) != 0)
            {
                if ((flags & FLAGS_PRECISION) == 0 && len != 0 && ((len == prec) || (len == width)))
                {
                    len--;
                    if (len != 0 && (_base == 16U))
                    {
                        len--;
                    }
                }
                if ((_base == 16U) && (flags & FLAGS_UPPERCASE) == 0 && (len < 32))
                {
                    buf[(int)len++] = 'x';
                }
                else if ((_base == 16U) && (flags & FLAGS_UPPERCASE) != 0 && (len < 32))
                {
                    buf[(int)len++] = 'X';
                }
                else if ((_base == 2U) && (len < 32))
                {
                    buf[(int)len++] = 'b';
                }
                if (len < 32)
                {
                    buf[(int)len++] = '0';
                }
            }

            if (len < 32)
            {
                if (negative)
                {
                    buf[(int)len++] = '-';
                }
                else if ((flags & FLAGS_PLUS) != 0)
                {
                    buf[(int)len++] = '+';  // ignore the space if the '+' exists
                }
                else if ((flags & FLAGS_SPACE) != 0)
                {
                    buf[(int)len++] = ' ';
                }
            }

            return _out_rev(_out, buffer, idx, buf, len, width, flags);
        }

        static readonly double[] pow10 = new double[] { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

        // internal ftoa for fixed decimal floating point
        static size_t _ftoa(out_fct_type _out, Stream buffer, size_t idx, double value, uint prec, uint width, uint flags)
        {
            char[] buf = new char[32];
            size_t len = 0U;
            double diff = 0.0;

            // powers of 10
            

            // test for special values
            if (value == float.NaN)
                return _out_rev(_out, buffer, idx, "nan".ToCharArray(), 3, width, flags);
            if (value < -Double.MaxValue)
                return _out_rev(_out, buffer, idx, "fni-".ToCharArray(), 4, width, flags);
            if (value > Double.MaxValue)
                return _out_rev(_out, buffer, idx, (flags & FLAGS_PLUS) != 0 ? "fni+".ToCharArray() : "fni".ToCharArray(), (flags & FLAGS_PLUS) != 0 ? 4U : 3U, width, flags);

            // test for very large values
            // standard printf behavior is to print EVERY whole number digit -- which could be 100s of characters overflowing your buffers == bad
            if ((value > 1e9) || (value < -1e9))
            {
//#if defined(PRINTF_SUPPORT_EXPONENTIAL)
                //return _etoa(_out, buffer, idx, value, prec, width, flags);
//#else
              return 0U;
//#endif
            }

            // test for negative
            bool negative = false;
            if (value < 0)
            {
                negative = true;
                value = 0 - value;
            }

            // set default precision, if not set explicitly
            if ((flags & FLAGS_PRECISION) == 0)
            {
                prec = 6U;
            }
            // limit precision to 9, cause a prec >= 10 can lead to overflow errors
            while ((len < 32) && (prec > 9U))
            {
                buf[(int)len++] = '0';
                prec--;
            }

            int whole = (int)value;
            double tmp = (value - whole) * pow10[prec];
            uint frac = (uint)tmp;
            diff = tmp - frac;

            if (diff > 0.5)
            {
                ++frac;
                // handle rollover, e.g. case 0.99 with prec 1 is 1.0
                if (frac >= pow10[prec])
                {
                    frac = 0;
                    ++whole;
                }
            }
            else if (diff < 0.5)
            {
            }
            else if ((frac == 0U) || (frac & 1U) != 0)
            {
                // if halfway, round up if odd OR if last digit is 0
                ++frac;
            }

            if (prec == 0U)
            {
                diff = value - (double)whole;
                if ((!(diff < 0.5) || (diff > 0.5)) && (whole & 1) != 0)
                {
                    // exactly 0.5 and ODD, then round up
                    // 1.5 -> 2, but 2.5 -> 2
                    ++whole;
                }
            }
            else
            {
                uint count = prec;
                // now do fractional part, as an unsigned number
                while (len < 32)
                {
                    --count;
                    buf[(int)len++] = (char)(48U + (frac % 10U));
                    if ((frac /= 10U) == 0)
                    {
                        break;
                    }
                }
                // add extra 0s
                while ((len < 32) && (count-- > 0U))
                {
                    buf[(int)len++] = '0';
                }
                if (len < 32)
                {
                    // add decimal
                    buf[(int)len++] = '.';
                }
            }

            // do whole part, number is reversed
            while (len < 32)
            {
                buf[(int)len++] = (char)(48 + (whole % 10));
                if ((whole /= 10) == 0)
                {
                    break;
                }
            }

            // pad leading zeros
            if ((flags & FLAGS_LEFT) == 0 && (flags & FLAGS_ZEROPAD) != 0)
            {
                if (width != 0 && (negative || (flags & (FLAGS_PLUS | FLAGS_SPACE)) != 0))
                {
                    width--;
                }
                while ((len < width) && (len < 32))
                {
                    buf[(int)len++] = '0';
                }
            }

            if (len < 32)
            {
                if (negative)
                {
                    buf[(int)len++] = '-';
                }
                else if ((flags & FLAGS_PLUS) != 0)
                {
                    buf[(int)len++] = '+';  // ignore the space if the '+' exists
                }
                else if ((flags & FLAGS_SPACE) != 0)
                {
                    buf[(int)len++] = ' ';
                }
            }

            return _out_rev(_out, buffer, idx, buf, len, width, flags);
        }


        // internal itoa for 'long' type
        static size_t _ntoa_long(out_fct_type _out, Stream buffer, size_t idx, uint value, bool negative, uint _base, uint prec, uint width, uint flags)
        {
            char[] buf = new char[32];
            size_t len = 0U;

            // no hash for 0 values
            if (value == 0)
            {
                flags &= ~FLAGS_HASH;
            }

            // write if precision != 0 and value is != 0
            if ((flags & FLAGS_PRECISION) == 0 || value !=0)
            {
                do
                {
                    char digit = (char)(value % _base);
                    buf[(int)len++] = (char)(digit < 10 ? '0' + digit : ((flags & FLAGS_UPPERCASE) != 0 ? 'A' : 'a') + digit - 10);
                    value /= _base;
                } while (value != 0 && (len < 32));
            }

            return _ntoa_format(_out, buffer, idx, buf, len, negative, (uint)_base, prec, width, flags);
        }

        static size_t _ntoa_long_long(out_fct_type _out, Stream buffer, size_t idx, ulong value, bool negative, uint _base, uint prec, uint width, uint flags)
        {
            char[] buf = new char[32];
            size_t len = 0U;

            // no hash for 0 values
            if (value == 0)
            {
                flags &= ~FLAGS_HASH;
            }

            // write if precision != 0 and value is != 0
            if ((flags & FLAGS_PRECISION) == 0 || value != 0)
            {
                do
                {
                    char digit = (char)(value % _base);
                    buf[(int)len++] = (char)(digit < 10 ? '0' + digit : ((flags & FLAGS_UPPERCASE) != 0 ? 'A' : 'a') + digit - 10);
                    value /= _base;
                } while (value != 0 && (len < 32));
            }

            return _ntoa_format(_out, buffer, idx, buf, len, negative, (uint)_base, prec, width, flags);
        }

        static int _vsprintf(out_fct_type _out, Stream buffer, string format, params object[] va)
        {
            uint flags, width, precision, n, formatPos = 0;
            int curParam = 0;
            size_t idx = 0U;

            if (buffer == null)
            {
                // use null output function
                _out = _out_null;
            }

            while (formatPos < format.Length)
            {
                // format specifier?  %[flags][width][.precision][length]
                if (format[(int)formatPos] != '%')
                {
                    // no
                    _out(format[(int)formatPos], buffer, idx++);
                    formatPos++;
                    continue;
                }
                else
                {
                    // yes, evaluate it
                    formatPos++;
                }

                // evaluate flags
                flags = 0U;
                do
                {
                    switch (format[(int)formatPos])
                    {
                        case '0': flags |= FLAGS_ZEROPAD; formatPos++; n = 1U; break;
                        case '-': flags |= FLAGS_LEFT; formatPos++; n = 1U; break;
                        case '+': flags |= FLAGS_PLUS; formatPos++; n = 1U; break;
                        case ' ': flags |= FLAGS_SPACE; formatPos++; n = 1U; break;
                        case '#': flags |= FLAGS_HASH; formatPos++; n = 1U; break;
                        default: n = 0U; break;
                    }
                } while (n != 0);

                // evaluate width field
                width = 0U;
                if (char.IsDigit(format[(int)formatPos]))
                {
                    width = _atoi(ref format, ref formatPos);
                }
                else if (format[(int)formatPos] == '*')
                {
                    try
                    {
                        int w = (int)va[curParam++];
                        if (w < 0)
                        {
                            flags |= FLAGS_LEFT;    // reverse padding
                            width = (uint)-w;
                        }
                        else
                        {
                            width = (uint)w;
                        }
                    }
                    catch (Exception e)
                    {
                        _out(format[(int)formatPos], buffer, idx++);
                        formatPos++;
                    }
                    formatPos++;
                }

                // evaluate precision field
                precision = 0U;
                if (format[(int)formatPos] == '.')
                {
                    try {
                        flags |= FLAGS_PRECISION;
                        formatPos++;
                        if (char.IsDigit(format[(int)formatPos]))
                        {
                            precision = _atoi(ref format, ref formatPos);
                        }
                        else if (format[(int)formatPos] == '*')
                        {
                            int prec = (int)va[curParam++];
                            precision = prec > 0 ? (uint)prec : 0U;
                            formatPos++;
                        }
                    }
                    catch (Exception e)
                    {
                        _out(format[(int)formatPos], buffer, idx++);
                        formatPos++;
                    }
                }

                // evaluate length field
                switch (format[(int)formatPos])
                {
                    case 'l':
                        flags |= FLAGS_LONG;
                        formatPos++;
                        if (format[(int)formatPos] == 'l')
                        {
                            flags |= FLAGS_LONG_LONG;
                            formatPos++;
                        }
                        break;
                    case 'h':
                        flags |= FLAGS_SHORT;
                        formatPos++;
                        if (format[(int)formatPos] == 'h')
                        {
                            flags |= FLAGS_CHAR;
                            formatPos++;
                        }
                        break;
                    case 'j':
                        flags |= (sizeof(long) == sizeof(long) ? FLAGS_LONG : FLAGS_LONG_LONG);
                        formatPos++;
                        break;
                    case 'z':
                        unsafe // this is unsafe apparently??????
                        {
                            flags |= (sizeof(size_t) == sizeof(long) ? FLAGS_LONG : FLAGS_LONG_LONG);
                        }
                        formatPos++;
                        break;
                    default:
                        break;
                }

                // evaluate specifier
                switch (format[(int)formatPos])
                {
                    case 'd':
                    case 'i':
                    case 'u':
                    case 'x':
                    case 'X':
                    case 'o':
                    case 'b':
                        {
                            try {
                                // set the _base
                                uint _base;
                                if (format[(int)formatPos] == 'x' || format[(int)formatPos] == 'X')
                                {
                                    _base = 16U;
                                }
                                else if (format[(int)formatPos] == 'o')
                                {
                                    _base = 8U;
                                }
                                else if (format[(int)formatPos] == 'b')
                                {
                                    _base = 2U;
                                }
                                else
                                {
                                    _base = 10U;
                                    flags &= ~FLAGS_HASH;   // no hash for dec format
                                }
                                // uppercase
                                if (format[(int)formatPos] == 'X')
                                {
                                    flags |= FLAGS_UPPERCASE;
                                }

                                // no plus or space flag for u, x, X, o, b
                                if ((format[(int)formatPos] != 'i') && (format[(int)formatPos] != 'd'))
                                {
                                    flags &= ~(FLAGS_PLUS | FLAGS_SPACE);
                                }

                                // ignore '0' flag when precision is given
                                if ((flags & FLAGS_PRECISION) != 0)
                                {
                                    flags &= ~FLAGS_ZEROPAD;
                                }
                                #pragma warning disable CS8500 
                                // convert the integer
                                if ((format[(int)formatPos] == 'i') || (format[(int)formatPos] == 'd'))
                                {
                                    // signed
                                    if ((flags & FLAGS_LONG_LONG) != 0)
                                    {
                                        long value = (long)va[curParam++];
                                        idx = _ntoa_long_long(_out, buffer, idx, (ulong)(value > 0 ? value : 0 - value), value < 0, _base, precision, width, flags);
                                    }
                                    else if ((flags & FLAGS_LONG) != 0)
                                    {
                                        int value = (int)va[curParam++];
                                        idx = _ntoa_long(_out, buffer, idx, (uint)(value > 0 ? value : 0 - value), value < 0, _base, precision, width, flags);
                                    }
                                    else
                                    {
                                        unsafe
                                        {
                                            //Debug.Log("Type: " + va[curParam].GetType().Name);
                                            //fixed (object* ptr = &va[curParam++])
                                            {
                                                object param = va[curParam++];
                                                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(param));
                                                Marshal.StructureToPtr(param, ptr, true);
                                                int value = (flags & FLAGS_CHAR) != 0 ?
                                                    *((sbyte*)ptr) : (flags & FLAGS_SHORT) != 0 ?
                                                    *((short*)ptr) :
                                                    *((int*)ptr);
                                                value = (value > 0 ? value : 0 - value);
                                                idx = _ntoa_long(_out, buffer, idx, *((uint*)&value), value < 0, _base, precision, width, flags);
                                                Marshal.FreeHGlobal(ptr);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // unsigned
                                    if ((flags & FLAGS_LONG_LONG) != 0)
                                    {
                                        idx = _ntoa_long_long(_out, buffer, idx, (ulong)va[curParam++], false, _base, precision, width, flags);
                                    }
                                    else if ((flags & FLAGS_LONG) != 0)
                                    {
                                        idx = _ntoa_long(_out, buffer, idx, (uint)va[curParam++], false, _base, precision, width, flags);
                                    }
                                    else
                                    {
                                        //uint value = (flags & FLAGS_CHAR) != 0 ? unchecked((byte)va[curParam++]) : (flags & FLAGS_SHORT) != 0 ? unchecked((ushort)va[curParam++]) : unchecked((uint)va[curParam++]);
                                        //idx = _ntoa_long(_out, buffer, idx, value, false, _base, precision, width, flags);
                                        unsafe
                                        {
                                            //Debug.Log("Type: " + va[curParam].GetType().Name);
                                            //fixed (object* ptr = &va[curParam++])
                                            {
                                                object param = va[curParam++];
                                                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(param));
                                                Marshal.StructureToPtr(param, ptr, true);
                                                uint value = (flags & FLAGS_CHAR) != 0 ?
                                                    *((byte*)ptr) : (flags & FLAGS_SHORT) != 0 ?
                                                    *((ushort*)ptr) :
                                                    *((uint*)ptr);
                                                idx = _ntoa_long(_out, buffer, idx, (value > 0 ? value : 0 - value), value < 0, _base, precision, width, flags);
                                                Marshal.FreeHGlobal(ptr);
                                            }
                                        }
                                    }
                                }
                                formatPos++;
                            }
                            catch (Exception e)
                            {
                                //Debug.LogError(e);//Util.err_printf("%s", e.ToString());
                                _out(format[(int)formatPos], buffer, idx++);
                                formatPos++;
                            }
                            break;
                        }
                    //#if defined(PRINTF_SUPPORT_FLOAT)
                    case 'f':
                    case 'F':
                        //try {
                        {
                            if (format[(int)formatPos] == 'F') flags |= FLAGS_UPPERCASE;
                            object param = va[curParam++];
                            if (param.GetType() == typeof(float))
                            {
                                float val = (float)param;
                                idx = _ftoa(_out, buffer, idx, (double)val, precision, width, flags);
                            }
                            else
                            {
                                idx = _ftoa(_out, buffer, idx, (double)param, precision, width, flags);
                            }
                            formatPos++;
                        }
                        //}
                        //catch (Exception e)
                        //{
                        //    _out(format[(int)formatPos], buffer, idx++);
                        //    formatPos++;
                        //}
                        break;
                    //#if defined(PRINTF_SUPPORT_EXPONENTIAL)
                    //case 'e':
                    //case 'E':
                    //case 'g':
                    //case 'G':
                    //    if ((format[(int)formatPos] == 'g') || (format[(int)formatPos] == 'G')) flags |= FLAGS_ADAPT_EXP;
                    //    if ((format[(int)formatPos] == 'E') || (format[(int)formatPos] == 'G')) flags |= FLAGS_UPPERCASE;
                    //    idx = _etoa(_out, buffer, idx, (double)va[curParam++], precision, width, flags);
                    //    formatPos++;
                    //    break;
                    //#endif  // PRINTF_SUPPORT_EXPONENTIAL
                    //#endif  // PRINTF_SUPPORT_FLOAT
                    case 'c':
                        {
                            try {
                                uint l = 1U;
                                // pre padding
                                if ((flags & FLAGS_LEFT) == 0)
                                {
                                    while (l++ < width)
                                    {
                                        _out(' ', buffer, idx++);
                                    }
                                }
                                // char output
                                _out((char)va[curParam++], buffer, idx++);
                                // post padding
                                if ((flags & FLAGS_LEFT) != 0)
                                {
                                    while (l++ < width)
                                    {
                                        _out(' ', buffer, idx++);
                                    }
                                }
                                formatPos++;
                            } catch (Exception e)
                                {
                                _out(format[(int)formatPos], buffer, idx++);
                                formatPos++;
                            }
                        break;
                        }

                    case 's':
                        {
                            try
                            {
                                string p = (string)va[curParam++];
                                uint l = (uint)p.Length;
                                // pre padding
                                if ((flags & FLAGS_PRECISION) != 0)
                                {
                                    l = (l < precision ? l : precision);
                                }
                                if ((flags & FLAGS_LEFT) == 0)
                                {
                                    while (l++ < width)
                                    {
                                        _out(' ', buffer, idx++);
                                    }
                                }
                                // string output
                                uint strPos = 0;
                                while ((strPos+1 < l) && (((flags & FLAGS_PRECISION) == 0) || (precision-- != 0)))
                                {
                                    _out((p[(int)strPos++]), buffer, idx++);
                                }
                                // post padding
                                if ((flags & FLAGS_LEFT) != 0)
                                {
                                    while (l++ < width)
                                    {
                                        _out(' ', buffer, idx++);
                                    }
                                }
                                formatPos++;
                            } catch (Exception e)
                            {
                                _out(format[(int)formatPos], buffer, idx++);
                                formatPos++;
                            }
                            break;
                        }

                    case 'p':
                        {
                            curParam++;
//                            width = (uint)sizeof(IntPtr) * 2U;
//                            flags |= FLAGS_ZEROPAD | FLAGS_UPPERCASE;
//#if defined(PRINTF_SUPPORT_LONG_LONG)
//        const bool is_ll = sizeof(uintptr_t) == sizeof(long long);
//        if (is_ll) {
//          idx = _ntoa_long_long(_out, buffer, idx, maxlen, (uintptr_t)va_arg(va, void*), false, 16U, precision, width, flags);
//        }
//        else {
//#endif
//                            idx = _ntoa_long(_out, buffer, idx, maxlen, (ulong)((uintptr_t)va_arg(va, void *)), false, 16U, precision, width, flags);
//#if defined(PRINTF_SUPPORT_LONG_LONG)
//        }
//#endif
//                            formatPos++;
                            break;
                        }

                    case '%':
                        _out('%', buffer, idx++);
                        formatPos++;
                        break;

                    default:
                        _out(format[(int)formatPos], buffer, idx++);
                        formatPos++;
                        break;
                }
            }

            // termination
            _out((char)0, buffer, idx);

            // return written chars without terminating \0
            return (int)idx;
        }

        static MemoryStream outStream = new MemoryStream();

        [System.Diagnostics.Conditional("DEBUG")]
        public static void dbg_printf(string format, params object[] va)
        {
            printf(format, va);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void err_printf(string format, params object[] va)
        {
            var outStream = new MemoryStream();
            _vsprintf(_out_char_err, outStream, format, va);
            outStream.Dispose();
            return;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void wrn_printf(string format, params object[] va)
        {
            var outStream = new MemoryStream();
            _vsprintf(_out_char_wrn, outStream, format, va);
            outStream.Dispose();
            return;
        }

        public static void printf(string format, params object[] va)
        {
            var outStream = new MemoryStream();
            _vsprintf(_out_char, outStream, format, va);
            outStream.Dispose();
            return;
        }

        public static string sprintf(string format, params object[] va)
        {
            var outStream = new MemoryStream();
            _vsprintf(_out_buffer, outStream, format, va);
            outStream.Position = 0;
            StreamReader reader = new StreamReader(outStream);
            string str = reader.ReadToEnd();
            outStream.Dispose();
            return str;
        }
    }
}