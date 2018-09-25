# 百度网盘OAuth模拟登陆过程

如无说明，每个HTTP请求的Header中会添加Chrome的User Agent：`Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36`

## 1. 获取网盘首页

`GET https://pan.baidu.com/`

HTTP Response Header

获得一个Set-Cookie字段 `BAIDUID`


## 2. 请求token

`GET https://passport.baidu.com/v2/api?getapi&`

HTTP Request Header

|Key|Value|
|:--|:--|
|Referer|`https://pan.baidu.com/`|

Query Parameters

|Key|Value|
|:--|:--|
|tpl|`netdisk`|
|subpro|`netdisk_web`|
|apiver|`v3`|
|tt|当前的整型unixtimestamp，毫秒为单位|
|class|`login`|
|gid|随机的guid/uuid4，这里只有31位，去掉头一位即可（不处理的话也不影响登陆）|
|loginversion|`v4`|
|logintype|`basicLogin`|
|traceid||
|callback|`bd__cbs__xxxxxx` 可以固定值，也可以随机生成|

注意参数的拼接，这里是
`/api?getapi&tpl=netdisk&...`
，而不是
`/api?getapi=&tpl=netdisk&...`

HTTP Response

```javascript
bd__cbs__xxxxxx({"errInfo":{        "no": "0"    },    "data": {        "rememberedUserName" : "",        "codeString" : "",        "token" : "f505be7b30b05da6983c06e95e5fcb54",        "cookie" : "1",        "usernametype":"",        "spLogin" : "rate",        "disable":"",        "loginrecord":{            'email':[            ],            'phone':[            ]        }    },    "traceid": ""})
```

登陆需要这里的`token`字段

## 3. 预登陆检测（这一步可选）

`GET https://passport.baidu.com/v2/api/?logincheck&`

HTTP Request Header

|Key|Value|
|:--|:--|
|Referer|`https://pan.baidu.com/`|

Query Parameters

|Key|Value|
|:--|:--|
|token|上面获得到的token|
|tpl|`netdisk`|
|subpro|`netdisk_web`|
|apiver|`v3`|
|tt|整形unixtimestamp，毫秒为单位|
|sub_source|`leadsetpwd`|
|username|用户名/邮箱/手机号|
|loginversion|`v4`|
|dv|由js代码生成的一段随机字符串，这里随便填填就好了，没看js代码，也不影响登陆，我使用的是`i_do_not_know_this_param`|
|traceid||
|callback|`bd__cbs__xxxxxx`|

HTTP Response

```javascript
bd__cbs__xxxxxx({"errInfo":{        "no": "0"    },    "data": {        "codeString" : "",        "vcodetype" : "",        "userid" : "",        "mobile" : "",        "displayname": "",        "isconnect": ""    },    "traceid": ""})
```

注意`vcodetype`和`codestring`字段，若不为空，则需要获取验证码

## 4. 获取RSA公钥

`GET https://passport.baidu.com/v2/getpublickey?`

HTTP Request Header

|Key|Value|
|:--|:--|
|Referer|`https://pan.baidu.com/`|

Query Parameters

|Key|Value|
|:--|:--|
|token|上面获取的token|
|tpl|`netdisk`|
|subpro|`netdisk_web`|
|apiver|`v3`|
|tt|整形unixtimestamp，毫秒为单位|
|gid|与1.请求时的gid一致（不一致也不影响）|
|loginversion|`v4`|
|traceid||
|callback|`bd__cbs__xxxxxx`|

HTTP Response

```javascript
bd__cbs__d04gyl({"errno":'0',"msg":'',"pubkey":'-----BEGIN PUBLIC KEY-----\nMIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCozAe3vjpmjh2kE5eDmo5qRRot\nruZHjDTXeS5Iky9km4nnOG8Mw9FkAUH8r4ITo28vhjV0RXyJuuMh2brbCE2pgmQb\nahlKyiaVuprsqqpZSxwsj4LVRY6\/dNcceYJawrbCAYyLiDlLf+ZBY\/m1nKFCl1QS\nX1YY2cPp2RCgOrWdSQIDAQAB\n-----END PUBLIC KEY-----\n',"key":'HP3Mub4Uf3kNp0oOzR0g6KEvOUN3JXPX',    "traceid": ""})
```

这里拿到两个参数`pubkey`和`key`，`pubkey`是对明文密码进行RSA加密用的，`key`需要在登陆时传入

## 5. 发送登陆请求

`POST https://passport.baidu.com/v2/api/?login`

HTTP Request Header

|Key|Value|
|:--|:--|
|Origin|`https://pan.baidu.com`|
|Referer|`https://pan.baidu.com/`|

HTTP Request Body
类型：`application/x-www-form-urlencoded`

|Key|Value|
|:--|:--|
|staticpage|`https://pan.baidu.com/res/static/thirdparty/pass_v3_jump.html`|
|charset|`UTF-8`|
|token|上面获取的token|
|tpl|`netdisk`|
|subpro|`netdisk_web`|
|apiver|`v3`|
|tt|整形unixtimestamp，毫秒为单位|
|codestring|验证码的codestring，无验证码时为空|
|safeflg|`0`|
|u|`https://pan.baidu.com/disk/home`|
|isPhone||
|detect|`1`|
|gid|上面的gid|
|quick_user|`0`|
|logintype|`basicLogin`|
|logLoginType|`pc_loginBase`|
|idc||
|loginmerge|`true`|
|foreignusername||
|username|用户名/手机/邮箱|
|password|RSA加密后密码的base64字符串|
|verifycode|需要验证码时传入验证码显示的内容，不需要时该字段不存在|
|mem_pass|`on`|
|rsakey|4.中的key|
|crypttype|`12`|
|ppui_logintime|登陆所用时长，毫秒为单位|
|contrycode||
|fp_uid||
|fp_info||
|loginversion|`v4`|
|dv|js随机生成的字符串，不影响登陆|
|vcodefrom|`login`|
|traceid|不知道从哪来的，可以为空|
|callback|`parent.bd__pcbs__xxxxxx`|

HTTP Response

```html
<!DOCTYPE html>
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
</head>
<body>
<script type="text/javascript">


	var href = decodeURIComponent("https:\/\/pan.baidu.com\/res\/static\/thirdparty\/pass_v3_jump.html")+"?"

var accounts = '&accounts='

href += "err_no=6&callback=parent.bd__pcbs__m3cd6i&codeString=jxGad07c1495344c172020b15e044014631c29f4307ce017f88&userName=xxxx&phoneNumber=&mail=&hao123Param=&u=https://pan.baidu.com/disk/home&tpl=netdisk&secstate=&gotourl=&authtoken=&loginproxy=&resetpwd=&vcodetype=63a9yp9QX5v+8r\/4UuqSRxlrXabX\/wlg5hitDnd5IrlhDu+J2l+sjPAnv7\/U8bmS9hO6ixloqx6HtSfSTQSRft2AGdYe0l+atdS9&lstr=&ltoken=&bckv=&bcsync=&bcchecksum=&code=&bdToken=&realnameswitch=&setpwdswitch=&bctime=&bdstoken=&authsid=&jumpset=&appealurl=&realnameverifyemail=0&traceid=6D597B01&realnameauthsid="+accounts;


if(window.location){
    window.location.replace(href);
}else{
   document.location.replace(href); 
}
</script>
</body>
</html>
```

需要的是检查`err_no`，`codeString`和`vcodetype`这三个字段
`err_no`用于判断登陆是否成功，比较常用的：0为成功，4为账号/密码错误，6为验证码错误，257为需要验证码
`codeString`和`vcodetype`用于验证码的获取

## 6. 获取验证码
`GET https://passport.baidu.com/cgi-bin/genimage?codestring`

`codestring`处填你上面获取到的codestring

HTTP Request Header

|Key|Value|
|:--|:--|
|Referer|`https://pan.baidu.com/`|

HTTP Response

一个图片

## 7. 刷新验证码

`GET https://passport.baidu.com/v2/reggetcodestr&`

HTTP Request Header

|Key|Value|
|:--|:--|
|Referer|`https://pan.baidu.com/`|

Query Parameters


HTTP Request Header

|Key|Value|
|:--|:--|
|token|上面获取的token|
|tpl|`netdisk`|
|subpro|`netdisk_web`|
|apiver|`v3`|
|tt|整形unixtimestamp，以毫秒为单位|
|fr|`login`|
|loginversion|`v4`|
|vcodetype|上面获取的vcodetype|
|traceid||
|callback|`bd__cbs__xxxxxx`|

HTTP Response

```javascript
bd__cbs__xxxxxx({"errInfo":{        "no": "0"    },    "data": {        "verifyStr" : "jxGfe07c14953f7c14b029415724301487fc39f4307a0017e3d",        "verifySign" : ""    },    "traceid": "6D597B01"})
```

这里获得是`verifyStr`就是新的验证码的`codestring`，把它传递到6.中即可
