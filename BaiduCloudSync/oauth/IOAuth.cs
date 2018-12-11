using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 百度开放授权接口
    /// </summary>
    public interface IOAuth
    {
        /// <summary>
        /// 登陆
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="captcha">验证码</param>
        /// <returns></returns>
        bool Login(string username, string password, object captcha = null);
        /// <summary>
        /// 登出
        /// </summary>
        /// <returns></returns>
        bool Logout();
        /// <summary>
        /// 获取验证码
        /// </summary>
        /// <returns></returns>
        object GetCaptcha();
        /// <summary>
        /// 是否已登录
        /// </summary>
        /// <returns></returns>
        bool IsLogin { get; }
        /// <summary>
        /// 获取BAIDUID的cookie值
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotLoggedInException">在未登陆时访问该值时引发的异常</exception>
        string GetBaiduID { get; }
        /// <summary>
        /// 获取BDUSS的cookie值
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotLoggedInException">在未登陆时访问该值时引发的异常</exception>
        string GetBDUSS { get; }
        /// <summary>
        /// 获取STOKEN的cookie值
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotLoggedInException">在未登陆时访问该值时引发的异常</exception>
        string GetSToken { get; }

    }
}
