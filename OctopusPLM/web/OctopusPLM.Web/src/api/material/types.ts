export type MaterialStatus = "active" | "inactive";
export interface MaterialRow { materialId:number; materialCode:string; materialName:string; specification?:string; baseUnit:string; materialType:string; status:MaterialStatus }
export interface MaterialWriteRequest { materialCode:string; materialName:string; specification?:string; baseUnit:string; materialType:string; status:MaterialStatus }
export interface MaterialPage { rows:MaterialRow[]; total:number; page:number; pageSize:number }
export interface MaterialReferences { manufacturingBomCount:number; templateItemCount:number }
