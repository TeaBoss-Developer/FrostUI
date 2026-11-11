# FrostUI for the Web

FrostUI 的 Web 版本：一套无依赖的玻璃材质 UI 组件库，支持深/浅主题、5 套强调色与运行时玻璃浓度。

- 纯 CSS + 原生 JS，无构建、无第三方依赖
- `backdrop-filter` 真实模糊，附带不支持时的降级表面
- 主题 / 强调色 / 模糊度写入 `localStorage`，可跨页面保持
- 组件：卡片、按钮、输入框、开关、滑杆、分段选择、标签、状态点、头像、统计块、导航、Toast、代码块
- 无障碍：焦点环、`prefers-reduced-motion`、`aria-pressed` 状态

## 安装

把 `frostui.css` 和 `frostui.js` 复制到项目里，或直接用 npm：

```bash
npm install frostui-web
```

```js
import 'frostui-web/frostui.css';
import 'frostui-web';                 // 执行脚本，注册全局 window.FrostUI

const { setTheme, setAccent } = window.FrostUI;
```

没有打包工具时：

```html
<link rel="stylesheet" href="frostui.css">
<script src="frostui.js" defer></script>
```

## 快速开始

```html
<html lang="zh-CN" data-f-theme="dark" data-f-accent="azure">
<body class="f-page">
  <a class="f-btn f-btn--primary" href="#">开始使用</a>

  <div class="f-card">
    <div class="f-card__head">
      <h3 class="f-card__title">玻璃卡片</h3>
    </div>
    <p class="f-muted">内容放在这里。</p>
  </div>
</body>
</html>
```

`f-page` 负责基础排版与背景；如果你的项目已有全局样式，可以只引入组件类，把 `f-page` 换成自己的容器。

## JS API

```js
FrostUI.setTheme('dark');      // 'dark' | 'light' | 'auto'
FrostUI.toggleTheme();
FrostUI.setAccent('mint');     // azure | mint | violet | sakura | amber
FrostUI.setBlur(32);           // 0–48px
FrostUI.toast('已保存');
FrostUI.copy('npm i frostui-web');
```

页面加载时会自动初始化并读取上次的偏好。也可以手动控制：

```js
FrostUI.init({ prefix: 'my-app' }); // 自定义 localStorage 前缀
```

### 自动绑定

| 属性 | 作用 |
| --- | --- |
| `data-f-theme-toggle` | 点击切换深浅色 |
| `data-f-theme-set="light"` | 设置为指定主题 |
| `data-f-accent="violet"` | 点击切换强调色 |
| `data-f-blur` | `<input type="range">`，拖动调整玻璃模糊 |
| `data-f-blur-out` | 显示当前模糊数值 |
| `data-f-copy="#code"` | 复制选择器指向元素的内容 |

## 主题令牌

所有视觉变量都以 `--f-` 开头，覆盖即可定制：

```css
:root {
  --f-accent: #84AEFF;
  --f-radius-lg: 20px;
  --f-blur: 24px;
}
```

完整令牌见 `frostui.css` 顶部第 1 节。

## 浏览器支持

Chromium / Edge 111+、Safari 16.4+、Firefox 113+ 体验最佳。
不支持 `backdrop-filter` 的浏览器会自动切换到不透明表面，布局不变。

## License

MIT © FrostUI contributors
