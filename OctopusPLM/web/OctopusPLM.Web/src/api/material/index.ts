import { createAuthedHttp } from "@/utils/plmHttp";
import type { MaterialPage, MaterialReferences, MaterialRow, MaterialWriteRequest } from "./types";
const http=createAuthedHttp("/api/materials");
export async function searchMaterials(params:Record<string,unknown>){ return (await http.get("",{params})).data.data as MaterialPage }
export async function createMaterial(data:MaterialWriteRequest){ return (await http.post("",data)).data as {code:number;msg:string;data:MaterialRow} }
export async function updateMaterial(id:number,data:MaterialWriteRequest){ return (await http.put(`/${id}`,data)).data as {code:number;msg:string;data:MaterialRow} }
export async function materialReferences(id:number){ return (await http.get(`/${id}/references`)).data.data as MaterialReferences }
export async function deleteMaterial(id:number){ return (await http.delete(`/${id}`)).data as {code:number;msg:string} }
