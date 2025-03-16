# 实时翻译器

一个基于.NET的实时翻译应用程序，支持多种翻译API，包括Microsoft翻译、Google翻译和有道翻译。

## 功能特点

- 支持多种翻译服务（Microsoft、Google、有道）
- 实时字幕捕获和翻译
- 悬浮窗显示翻译结果
- 自定义翻译设置（延迟、最大字符数等）
- 支持多种语言之间的互译

## 安装与设置

1. 克隆仓库到本地
```
git clone https://github.com/20f7fish/real-time-translator.git
```

2. 配置API密钥
   - 将`appsettings.example.json`复制为`appsettings.json`
   - 在`appsettings.json`中填入您的API密钥和相关配置

```json
{
  "GoogleCloud": {
    "ProjectId": "your-project-id",
    "CredentialsPath": "credentials/google-cloud-credentials.json"
  },
  "YoudaoCloud": {
    "AppKey": "your-youdao-app-key",
    "AppSecret": "your-youdao-app-secret"
  },
  "MicrosoftTranslator": {
    "SubscriptionKey": "your-subscription-key",
    "Region": "your-region"
  },
  // 其他配置...
}
```

3. 使用Visual Studio或.NET CLI构建项目
```
dotnet build
```

4. 运行应用程序
```
dotnet run
```

## 隐私与安全

- **重要提示**：请勿将包含API密钥的`appsettings.json`文件提交到公共仓库
- 项目已配置`.gitignore`文件，自动忽略`appsettings.json`和其他敏感文件
- 如需分享代码，请确保使用示例配置文件`appsettings.example.json`，不包含真实API密钥

## 获取API密钥

- **Microsoft翻译**：通过[Azure门户](https://portal.azure.com/)创建Translator资源
- **Google翻译**：通过[Google Cloud Console](https://console.cloud.google.com/)设置项目和API
- **有道翻译**：在[有道智云](https://ai.youdao.com/)申请API密钥

## 许可证

[MIT](LICENSE) 