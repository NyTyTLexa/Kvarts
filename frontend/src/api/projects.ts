// Контракты проектов (ТЗ п.5.2). camelCase — как сериализует ASP.NET Core.

export interface ProjectDto {
  id: string; name: string; rp?: string; createdAtUtc: string; dueDateUtc?: string
  specificationId?: string; specificationTitle?: string; itemsCount: number
  status: string; value?: number; marginPercent?: number
}
export interface CreateProjectRequest { name: string; rp?: string | null; dueDateUtc?: string | null; specificationId?: string | null }
