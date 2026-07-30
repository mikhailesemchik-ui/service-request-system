export interface Category {
  id: number;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCategoryRequest {
  name: string;
  description: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  description: string | null;
}

export interface UpdateCategoryActiveStateRequest {
  isActive: boolean;
}

export const CATEGORIES_PATH = '/api/categories';
