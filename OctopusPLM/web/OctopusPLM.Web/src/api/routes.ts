import { http } from "@/utils/http";

type Result = {
  success: boolean;
  data: Array<any>;
};

/** 获取动态路由：PLM 暂不使用后端动态路由，直接返回空数组 */
export const getAsyncRoutes = (): Promise<Result> => {
  return Promise.resolve({ success: true, data: [] });
};
