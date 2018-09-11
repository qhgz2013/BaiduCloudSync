using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlobalUtil.NetUtils;
using GlobalUtil;

namespace BaiduCloudSync.oauth
{
    public class OAuthPCWebImpl : IAuth
    {
        //用于区分不同账号的cookie所需要的key
        private string _identifier;
        public OAuthPCWebImpl(string identifier=null)
        {
            if (identifier == null)
            {
                //通过表单的随机生成算法生成当前cookie所属的key
                identifier = util.GenerateFormDataBoundary();
            }
            _identifier = identifier;
        }
        public object GetCaptcha()
        {
            throw new NotImplementedException();
        }

        public bool IsLogin()
        {
            throw new NotImplementedException();
        }

        public bool Login(string username, string password, object captcha = null)
        {
            return false;
        }

        public bool Logout()
        {
            throw new NotImplementedException();
        }
    }
}
