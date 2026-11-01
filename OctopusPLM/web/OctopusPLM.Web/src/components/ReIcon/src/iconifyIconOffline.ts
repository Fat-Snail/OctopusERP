import { h, defineComponent } from "vue";
import { Icon as IconifyIcon, addIcon } from "@iconify/vue/dist/offline";

// @iconify/vue 的 addIcon 类型签名只声明了 (name: string, data: IconifyIcon)，
// 而本组件沿用上游 pure-admin 的写法：icon 传对象时直接把该对象交给 addIcon 注册。
// 这里做一次局部放宽，运行时行为与升级前完全一致。
const registerIcon = addIcon as unknown as (
  name: unknown,
  data: unknown
) => void;

// Iconify Icon在Vue里本地使用（用于内网环境）
export default defineComponent({
  name: "IconifyIconOffline",
  components: { IconifyIcon },
  props: {
    icon: {
      default: null
    }
  },
  render() {
    if (typeof this.icon === "object") registerIcon(this.icon, this.icon);
    const attrs = this.$attrs;
    if (typeof this.icon === "string") {
      return h(
        IconifyIcon,
        {
          icon: this.icon,
          "aria-hidden": false,
          style: attrs?.style
            ? Object.assign(attrs.style, { outline: "none" })
            : { outline: "none" },
          ...attrs
        },
        {
          default: () => []
        }
      );
    } else {
      return h(
        this.icon,
        {
          "aria-hidden": false,
          style: attrs?.style
            ? Object.assign(attrs.style, { outline: "none" })
            : { outline: "none" },
          ...attrs
        },
        {
          default: () => []
        }
      );
    }
  }
});
