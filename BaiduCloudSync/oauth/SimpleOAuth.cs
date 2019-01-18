using BaiduCloudSync.oauth.exception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 使用指定的BAIDUID，BDUSS和STOKEN实现简单的OAuth接口
    /// </summary>
    public sealed class SimpleOAuth : IOAuth
    {
        private string _baidu_id, _bduss, _stoken;
        private DateTime _expiration_time;

        /// <summary>
        /// 使用默认的IOAuth接口构造SimpleOAuth
        /// </summary>
        /// <param name="auth">任意一种OAuth接口类型的实例</param>
        public SimpleOAuth(IOAuth auth)
        {
            if (auth == null)
                return;

            try
            {
                _baidu_id = auth.BaiduID;
                _bduss = auth.BDUSS;
                _stoken = auth.SToken;
                _expiration_time = auth.ExpirationTime;
            }
            catch (NotLoggedInException)
            {
                GlobalUtil.Tracer.GlobalTracer.TraceWarning("Trying to accessing non-logging-in OAuth instance, set oauth parameters to null");
                _baidu_id = null;
                _bduss = null;
                _stoken = null;
                _expiration_time = DateTime.MinValue;
            }
        }

        /// <summary>
        /// 构造一个不包含任何登录信息的OAuth接口类
        /// </summary>
        public SimpleOAuth() { }

        /// <summary>
        /// 按指定的BAIDUID、BDUSS和STOKEN的值实现OAuth接口类，当4个参数全为null时等同于SimpleOAuth()，否则要求三个参数不为空
        /// </summary>
        /// <param name="baidu_id">BAIDUID的cookie值</param>
        /// <param name="bduss">BDUSS的cookie值</param>
        /// <param name="stoken">STOKEN的cookie值</param>
        /// <param name="expiration_time">cookie的过期时间</param>
        /// <exception cref="ArgumentException">4个参数不全为空时引发的异常</exception>
        public SimpleOAuth(string baidu_id, string bduss, string stoken, DateTime expiration_time)
        {
            _set_args(baidu_id, bduss, stoken, expiration_time);
        }
        private void _set_args(string baidu_id, string bduss, string stoken, DateTime expiration_time)
        {
            bool all_null = baidu_id == null && bduss == null && stoken == null && expiration_time == DateTime.MinValue;
            bool all_non_null = !string.IsNullOrEmpty(baidu_id) && !string.IsNullOrEmpty(bduss) && !string.IsNullOrEmpty(stoken) && expiration_time != DateTime.MinValue;

            if (all_null || all_non_null)
            {
                _baidu_id = baidu_id;
                _bduss = bduss;
                _stoken = stoken;
                _expiration_time = expiration_time;
            }
            else
                throw new ArgumentException("Parameter baidu_id, bduss, stoken should be null or non-null at the same time");
        }
        public bool IsLogin
        {
            get
            {
                return !string.IsNullOrEmpty(_baidu_id) && _expiration_time > DateTime.Now;
            }
        }

        public string BaiduID
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return _baidu_id;
            }
        }

        public string BDUSS
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return _bduss;
            }
        }

        public string SToken
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return _stoken;
            }
        }

        public DateTime ExpirationTime
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return _expiration_time;
            }
        }

        public object GetCaptcha()
        {
            throw new NotSupportedException();
        }

        public bool Login(string username, string password, object captcha = null)
        {
            throw new NotSupportedException();
        }

        public bool Logout()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 更改OAuth的必要参数
        /// </summary>
        /// <param name="baidu_id">BAIDUID的cookie值</param>
        /// <param name="bduss">BDUSS的cookie值</param>
        /// <param name="stoken">STOKEN的cookie值</param>
        /// <param name="expiration_time">cookie的过期时间</param>
        /// <exception cref="ArgumentException">4个参数不全为空时引发的异常</exception>
        public void SetVariables(string baidu_id, string bduss, string stoken, DateTime expiration_time)
        {
            _set_args(baidu_id, bduss, stoken, expiration_time);
        }
    }
}
