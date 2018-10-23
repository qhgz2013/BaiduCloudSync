using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GlobalUtil.http
{
    public class CookieParser
    {
        public static Cookie ParseCookie(string set_cookie_string)
        {
            var ret = new Cookie();
            try
            {
                // set-cookie-header
                _rfc6265_set_cookie_header(set_cookie_string, ret);
            }
            catch (Exception)
            {
                throw new ArgumentException("cookie format error, not rfc-6265 syntax");
            }
            return ret;
        }


        private static string _rfc6265_set_cookie_header(string str, Cookie c)
        {
            if (string.IsNullOrEmpty(str))
                throw new ArgumentNullException("str");
            const string set_cookie_str = "set-cookie:";
            // "Set-Cookie:"
            if (str.Length <= set_cookie_str.Length)
                throw new ArgumentException("Empty Set-Cookie message");
            str = str.Substring(set_cookie_str.Length);
            // SP
            str = _rfc6265_sp(str);
            if (str.Length == 0)
                throw new ArgumentException("No key value found in Set-Cookie message");
            // set-cookie-string
            str = _rfc6265_set_cookie_string(str, c);
            return str;
        }

        private static string _rfc6265_sp(string str)
        {
            bool is_continue_match = true;
            // repetition rule *( [ obs-fold ] WSP)
            while (is_continue_match)
            {
                // "obs-fold" = CRLF
                bool cond1, cond2;
                str = _skip_char(str, "\r\n", out cond1);
                // WSP = " " or "\t" defined in RFC 5234
                str = _skip_char(str, " \t", out cond2);

                is_continue_match = cond1 || cond2;
            }
            return str;
        }

        private static string _skip_char(string str, string term_word, out bool suc)
        {
            int i = 0;
            while (i < str.Length && term_word.Contains(str[i])) i++;
            str = str.Substring(i);
            suc = i != 0;
            return str;
        }

        private static string _rfc6265_set_cookie_string(string str, Cookie c)
        {
            // cookie-pair
            str = _rfc6265_cookie_pair(str, c);

            // *( ";" SP cookie-av )
            while (true)
            {
                if (str.Length == 0) break;
                // ";"
                if (str[0] != ';')
                    throw new ArgumentException("Parse cookie failed, expected ';' (near '" + str + "')");
                str = str.Substring(1);

                // SP
                str = _rfc6265_sp(str);

                // cookie-av
                str = _rfc6265_cookie_av(str, c);
            }
            return str;
        }

        private static string _rfc6265_cookie_pair(string str, Cookie c)
        {
            // cookie-name
            str = _rfc6265_cookie_name(str, c);
            // "="
            if (str.Length == 0 || str[0] != '=')
                throw new ArgumentException("Parse cookie failed, expected '=' (near '" + str + "')");
            str = str.Substring(1);
            // cookie-value
            str = _rfc6265_cookie_value(str, c);
            return str;
        }
        private static string _rfc6265_cookie_name(string str, Cookie c)
        {
            // token, defined in RFC 2616 - 2.1
            string token;
            str = _rfc2616_token(str, out token);
            c.Name = token;
            return str;
        }

        private static string _rfc2616_token(string str, out string token)
        {
            // 1*<any CHAR except CTLs or seperators>

            // CTL: <any US-ASCII control character> (octets 0 - 31) (0 - 25 in decimal) and DEL (127)

            // separators
            const string seperator = "()<>@,;:\\\"/[]?={} \t";
            token = "";

            int i = 0;
            while (i < str.Length)
            {

                if (!seperator.Contains(str[i]) && !(str[i] < 26 || str[i] == 127))
                {
                    token += str[i];
                    i++;
                }
                else
                {
                    return str.Substring(i);
                }
            }
            // end of string
            return str.Substring(i);
        }

        private static string _rfc6265_cookie_value(string str, Cookie c)
        {
            // *cookie-octet
            bool suc;
            str = _rfc6265_cookie_octet(str, c, out suc);
            if (!suc)
            {
                // / (DQUOTE *cookie-octet DQUOTE)
                if (str.Length == 0 || str[0] != '"')
                    throw new ArgumentException("Parse cookie failed, expected '\"' (near '" + str + "')");
                str = str.Substring(1);
                str = _rfc6265_cookie_octet(str, c, out suc);
                if (!suc)
                    throw new ArgumentException("Parse cookie value failed");
                if (str.Length == 0 || str[0] != '"')
                    throw new ArgumentException("Parse cookie failed, expected '\"' (near '" + str + "')");
                str = str.Substring(1);
            }
            return str;
        }
        private static string _rfc6265_cookie_octet(string str, Cookie c, out bool suc)
        {
            string value = "";
            int i = 0;
            while (i < str.Length)
            {
                int ch = str[i];
                // %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
                if (ch == 0x21 || (ch >= 0x23 && ch <= 0x2b) || (ch >= 0x2d && ch <= 0x3a) || (ch >= 0x3c && ch <= 0x5b) || (ch >= 0x5d && ch <= 0x7e))
                    value += str[i];
                else
                    break;
                i++;
            }
            c.Value = value;
            suc = i != 0;
            return str.Substring(i);
        }


        private static string _rfc6265_cookie_av(string str, Cookie c)
        {
            // expires-av / max-age-av / domain-av / path-av / secure-av / httponly-av / extension-av
            string origin = str;
            bool suc;

            // expires-av
            str = _rfc6265_expires_av(str, c, out suc);
            if (suc)
                return str;

            // max-age-av
            str = _rfc6265_max_age_av(str, c, out suc);
            if (suc)
                return str;

            // domain-av
            str = _rfc6265_domain_av(str, c, out suc);
            if (suc)
                return str;

            // path-av
            str = _rfc6265_path_av(str, c, out suc);
            if (suc)
                return str;

            // secure-av
            str = _rfc6265_secure_av(str, c, out suc);
            if (suc)
                return str;

            // httponly-av
            str = _rfc6265_httponly_av(str, c, out suc);
            if (suc)
                return str;

            // extension-av
            string _;
            str = _rfc6265_path_value(str, out _, out suc);
            if (suc)
                return str;
            return origin;
        }

        private static string _rfc6265_expires_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string expires_av_str = "expires=";
            // "Expires="
            if (str.Length > expires_av_str.Length && str.Substring(0, expires_av_str.Length).ToLower() == expires_av_str)
            {
                str = str.Substring(expires_av_str.Length);
                // sane-cookie-date
                DateTime? date;
                str = _rfc6265_sane_cookie_date(str, out date, out suc);
                if (suc && c.Expires == DateTime.MinValue)
                    c.Expires = date.Value;
                if (suc)
                    return str;
                else
                    return origin;
            }
            else
            {
                suc = false;
                return origin;
            }
        }

        private static string _rfc6265_max_age_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string max_age_av_str = "max-age=";
            // "Max-age="
            if (str.Length > max_age_av_str.Length && str.Substring(0, max_age_av_str.Length).ToLower() == max_age_av_str)
            {
                str = str.Substring(max_age_av_str.Length);
                int max_age = 0;
                // non-zero-digit
                if (str.Length > 0 && str[0] >= '1' && str[0] <= '9')
                    max_age = int.Parse(str.Substring(0, 1));
                else
                {
                    suc = false;
                    return origin;
                }
                str = str.Substring(1);

                // *DIGIT
                while (str.Length > 0 && (str[0] >= '0' && str[0] <= '9'))
                {
                    max_age = max_age * 10 + int.Parse(str.Substring(0, 1));
                    str = str.Substring(1);
                }

                suc = true;
                c.Expires = DateTime.Now.AddSeconds(max_age);
                return str;
            }
            else
            {
                suc = false;
                return origin;
            }
        }

        private static string _rfc6265_domain_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string domain_av_str = "domain=";
            // "Domain="
            if (str.Length > domain_av_str.Length && str.Substring(0, domain_av_str.Length).ToLower() == domain_av_str)
            {
                str = str.Substring(domain_av_str.Length);
                // domain-value
                string domain;
                str = _rfc6265_domain_value(str, out domain, out suc);
                if (!suc)
                    return origin;
                c.Domain = domain;
                return str;
            }
            else
            {
                suc = false;
                return origin;
            }
        }
        private static string _rfc6265_path_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string path_av_str = "path=";
            // "Path="
            if (str.Length > path_av_str.Length && str.Substring(0, path_av_str.Length).ToLower() == path_av_str)
            {
                str = str.Substring(path_av_str.Length);
                // domain-value
                string path;
                str = _rfc6265_path_value(str, out path, out suc);
                if (!suc)
                    return origin;
                c.Path = path;
                return str;
            }
            else
            {
                suc = false;
                return origin;
            }
        }

        private static string _rfc6265_secure_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string secure_av_str = "secure";
            // "Secure"
            if (str.Length >= secure_av_str.Length && str.Substring(0, secure_av_str.Length).ToLower() == secure_av_str)
            {
                str = str.Substring(secure_av_str.Length);
                suc = true;
                c.Secure = true;
                return str;
            }
            else
            {
                suc = false;
                return origin;
            }
        }
        private static string _rfc6265_httponly_av(string str, Cookie c, out bool suc)
        {
            string origin = str;
            const string httponly_av_str = "httponly";
            // "HttpOnly"
            if (str.Length >= httponly_av_str.Length && str.Substring(0, httponly_av_str.Length).ToLower() == httponly_av_str)
            {
                str = str.Substring(httponly_av_str.Length);
                suc = true;
                c.HttpOnly = true;
                return str;
            }
            else
            {
                suc = false;
                return origin;
            }
        }
        private static string _rfc6265_path_value(string str, out string path, out bool suc)
        {
            // any CHAR except CTLs or ";"
            int i = 0;
            path = "";
            while (i < str.Length)
            {
                if (str[i] == ';' || (str[i] < 26 || str[i] == 127))
                    break;
                path += str[i];
                i++;
            }
            suc = i != 0;
            return str.Substring(i);
        }
        private static string _rfc6265_domain_value(string str, out string domain, out bool suc)
        {
            // subdomain
            string origin = str;
            suc = false;

            // defined in RFC1034 - 3.5, enhanced by RFC1123 - 2.1
            const string re_ipaddr = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])";
            //const string re_hostname = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])";
            const string re_hostname = @"^(([a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]|[a-zA-Z0-9])\.)*([A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9]|[A-Za-z0-9])";
            var match_ipaddr = Regex.Match(str, re_ipaddr);
            if (match_ipaddr.Success)
            {
                domain = match_ipaddr.Value;
                suc = true;
                return str.Substring(match_ipaddr.Value.Length);
            }

            bool lead_dot = false;
            if (str.Length > 0 && str[0] == '.')
            {
                lead_dot = true;
                str = str.Substring(1);
            }

            var match_hostname = Regex.Match(str, re_hostname);
            if (match_hostname.Success)
            {
                domain = (lead_dot ? "." : "") + match_hostname.Value;
                suc = true;
                return str.Substring(match_hostname.Value.Length);
            }
            domain = "";
            return origin;
        }

        private static string _rfc6265_sane_cookie_date(string str, out DateTime? date, out bool suc)
        {
            // rfc1123-date, defined in RFC2616 - 3.3.1

            // rfc1123-date | rfc850-date | asctime-date

            // rfc1123-date
            str = _rfc1123_date(str, out date, out suc);
            if (suc)
                return str;

            // rfc850-date
            str = _rfc850_date(str, out date, out suc);
            if (suc)
                return str;

            // asctime-date
            str = _asctime_date(str, out date, out suc);
            return str;
        }

        private static string _rfc850_date(string str, out DateTime? date, out bool suc)
        {
            // weekday "," SP date2 SP time SP "GMT"
            var origin = str;
            DateTime? temp_date = null;
            date = null;

            // weekday
            int wkday;
            str = _rfc2616_weekday(str, out wkday, out suc);
            if (!suc)
            {
                str = _rfc2616_wkday(str, out wkday, out suc);
                if (!suc)
                    return origin;
            }

            // ","
            suc = false;
            if (str.Length == 0 || str[0] != ',')
                return origin;
            str = str.Substring(1);

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // date2
            str = _rfc2616_date2(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // time
            str = _rfc2616_time(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // "GMT"
            suc = false;
            if (str.Length < 3 || str.Substring(0, 3).ToLower() != "gmt")
                return origin;
            str = str.Substring(3);

            // validating time (because 2 digit of year is not accurate enough)
            int current_century = DateTime.Now.Year / 100;
            DateTime validate_date;
            suc = false;
            for (int century = current_century; century < 100; century++)
            {
                validate_date = temp_date.Value.AddYears(century * 100);
                if ((int)validate_date.DayOfWeek == wkday)
                {
                    temp_date = validate_date;
                    suc = true;
                    break;
                }
            }
            if (!suc)
                return origin;

            suc = true;
            date = temp_date;
            return str;
        }

        private static string _asctime_date(string str, out DateTime? date, out bool suc)
        {
            // wkday SP date3 SP time SP 4DIGIT
            string origin = str;
            suc = false;
            date = null;
            DateTime? temp_date = null;

            // wkday
            int wkday;
            str = _rfc2616_weekday(str, out wkday, out suc);
            if (!suc)
            {
                str = _rfc2616_wkday(str, out wkday, out suc);
                if (!suc)
                    return origin;
            }

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // date3
            str = _rfc2616_date3(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // time
            str = _rfc2616_time(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // 4DIGIT
            suc = false;
            if (str.Length < 4)
                return origin;
            string year = str.Substring(0, 4);
            int int_year;
            if (!int.TryParse(year, out int_year))
                return origin;
            str = str.Substring(4);

            temp_date = temp_date.Value.AddYears(int_year - 1);
            date = temp_date;
            suc = true;
            return str;
        }
        private static string _rfc1123_date(string str, out DateTime? date, out bool suc)
        {
            string origin = str;
            // wkday
            DateTime? temp_date = null;
            date = null;
            suc = false;

            int wkday;
            str = _rfc2616_weekday(str, out wkday, out suc);
            if (!suc)
            {
                str = _rfc2616_wkday(str, out wkday, out suc);
                if (!suc)
                    return origin;
            }

            // ","
            suc = false;
            if (str.Length == 0 || str[0] != ',')
                return origin;
            str = str.Substring(1);

            // SP
            str = _rfc2616_sp(str, out suc);

            // date1
            str = _rfc2616_date1(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // time
            str = _rfc2616_time(str, ref temp_date, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            // "GMT"
            suc = false;
            if (str.Length < 3 || str.Substring(0, 3).ToLower() != "gmt")
                return origin;
            str = str.Substring(3);

            // validating weekday
            if ((int)temp_date.Value.DayOfWeek != wkday)
                return origin;

            suc = true;
            date = temp_date;
            return str;
        }

        private static string _rfc2616_date1(string str, ref DateTime? date, out bool suc)
        {
            string origin = str;
            // 2DIGIT SP month SP 4DIGIT
            suc = false;

            // 2DIGIT
            if (str.Length < 2)
                return origin;
            string day = str.Substring(0, 2);
            int int_day;
            if (!int.TryParse(day, out int_day))
                return origin;
            str = str.Substring(2);

            // SP | "-"
            str = _rfc2616_sp(str, out suc);
            if (!suc)
            {
                if (str.Length == 0 || str[0] != '-')
                    return origin;
                str = str.Substring(1);
            }

            // month
            int int_month;
            str = _rfc2616_month(str, out int_month, out suc);
            if (!suc)
                return origin;

            // SP | "-"
            str = _rfc2616_sp(str, out suc);
            if (!suc)
            {
                if (str.Length == 0 || str[0] != '-')
                    return origin;
                str = str.Substring(1);
            }

            // 4DIGIT
            suc = false;
            if (str.Length < 4)
                return origin;
            string year = str.Substring(0, 4);
            int int_year;
            if (!int.TryParse(year, out int_year))
                return origin;
            str = str.Substring(4);

            suc = true;
            if (date == null)
                date = new DateTime(int_year, int_month, int_day);
            else
            {
                var time = date.Value.TimeOfDay;
                date = new DateTime(date.Value.Date.Ticks) + time;
            }
            return str;
        }

        private static string _rfc2616_date2(string str, ref DateTime? date, out bool suc)
        {
            // 2DIGIT "-" month "-" 2DIGIT
            string origin = str;
            suc = false;

            // 2DIGIT
            if (str.Length < 2)
                return origin;
            string day = str.Substring(0, 2);
            int int_day;
            if (!int.TryParse(day, out int_day))
                return origin;
            str = str.Substring(2);

            // SP | "-"
            str = _rfc2616_sp(str, out suc);
            if (!suc)
            {
                if (str.Length == 0 || str[0] != '-')
                    return origin;
                str = str.Substring(1);
            }

            // month
            int int_month;
            str = _rfc2616_month(str, out int_month, out suc);
            if (!suc)
                return origin;

            // SP | "-"
            str = _rfc2616_sp(str, out suc);
            if (!suc)
            {
                if (str.Length == 0 || str[0] != '-')
                    return origin;
                str = str.Substring(1);
            }

            // 2DIGIT
            suc = false;
            if (str.Length < 2)
                return origin;
            string year = str.Substring(0, 2);
            int int_year;
            if (!int.TryParse(year, out int_year))
                return origin;
            str = str.Substring(2);

            suc = true;
            if (date == null)
                date = new DateTime(int_year, int_month, int_day);
            else
            {
                var time = date.Value.TimeOfDay;
                date = new DateTime(date.Value.Date.Ticks) + time;
            }
            return str;
        }
        private static string _rfc2616_date3(string str, ref DateTime? date, out bool suc)
        {
            // month SP ( 2DIGIT | (SP 1DIGIT ))
            string origin = str;

            // month
            int int_month;
            str = _rfc2616_month(str, out int_month, out suc);
            if (!suc)
                return origin;

            // SP
            str = _rfc2616_sp(str, out suc);
            if (!suc)
                return origin;

            int int_day;

            // 2DIGIT | (SP 1DIGIT)
            str = _rfc2616_sp(str, out suc);
            if (suc)
            {
                // 1DIGIT
                if (str.Length == 0 || !int.TryParse(str.Substring(0, 1), out int_day))
                {
                    suc = false;
                    return origin;
                }
                str = str.Substring(1);
            }
            else
            {
                // 2DIGIT
                if (str.Length < 2 || !int.TryParse(str.Substring(0, 2), out int_day))
                {
                    suc = false;
                    return origin;
                }
                str = str.Substring(2);
            }

            if (date == null)
                date = new DateTime(1, int_month, int_day);
            else
            {
                var time = date.Value.TimeOfDay;
                date = new DateTime(date.Value.Year, int_month, int_day) + time;
            }
            suc = true;
            return str;
        }

        private static string _rfc2616_month(string str, out int month, out bool suc)
        {
            string[] months = new string[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
            if (str.Length < 3)
            {
                suc = false;
                month = 0;
                return str;
            }
            var exact_month = str.Substring(0, 3);
            int int_month = 1;
            foreach (var mon in months)
            {
                if (mon == exact_month.ToLower())
                {
                    suc = true;
                    month = int_month;
                    return str.Substring(3);
                }
                int_month++;
            }
            suc = false;
            month = 0;
            return str;
        }
        private static string _rfc2616_time(string str, ref DateTime? date, out bool suc)
        {
            // 2DIGIT ":" 2DIGIT ":" 2DIGIT
            string origin = str;
            suc = false;

            // 2DIGIT
            if (str.Length < 2)
                return origin;
            string hour = str.Substring(0, 2);
            int int_hour;
            if (!int.TryParse(hour, out int_hour))
                return origin;
            str = str.Substring(2);

            // ":"
            suc = false;
            if (str.Length == 0 || str[0] != ':')
                return origin;
            str = str.Substring(1);

            // 2DIGIT
            if (str.Length < 2)
                return origin;
            string minute = str.Substring(0, 2);
            int int_minute;
            if (!int.TryParse(minute, out int_minute))
                return origin;
            str = str.Substring(2);

            // ":"
            suc = false;
            if (str.Length == 0 || str[0] != ':')
                return origin;
            str = str.Substring(1);

            // 2DIGIT
            if (str.Length < 2)
                return origin;
            string second = str.Substring(0, 2);
            int int_second;
            if (!int.TryParse(second, out int_second))
                return origin;
            str = str.Substring(2);

            if (date == null)
                date = new DateTime(1, 1, 1, int_hour, int_minute, int_second);
            else
            {
                var time = new TimeSpan(int_hour, int_minute, int_second);
                date = date.Value.Date + time;
            }
            suc = true;
            return str;
        }
        private static string _rfc2616_weekday(string str, out int weekday, out bool suc)
        {
            string[] days = new string[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
            int index = 0;
            foreach (var day in days)
            {
                if (str.Length < day.Length)
                    continue;
                var exact_day = str.Substring(0, day.Length);
                if (day == exact_day.ToLower())
                {
                    suc = true;
                    weekday = index;
                    return str.Substring(day.Length);
                }
                index++;
            }
            suc = false;
            weekday = -1;
            return str;
        }
        private static string _rfc2616_sp(string str, out bool suc)
        {
            suc = true;
            if (str.Length > 0 && str[0] == ' ')
                return str.Substring(1);
            suc = false;
            return str;
        }
        private static string _rfc2616_wkday(string str, out int wkday, out bool suc)
        {
            string[] days = new string[] { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
            if (str.Length < 3)
            {
                suc = false;
                wkday = -1;
                return str;
            }
            var exact_day = str.Substring(0, 3);
            int index = 0;
            foreach (var day in days)
            {
                if (day == exact_day.ToLower())
                {
                    suc = true;
                    wkday = index;
                    return str.Substring(3);
                }
                index++;
            }
            suc = false;
            wkday = -1;
            return str;
        }

    }
}
