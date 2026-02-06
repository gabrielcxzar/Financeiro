import axios from 'axios';

const api = axios.create({
  // Se tiver VITE_API_URL, usa ela. SenÃ£o, usa localhost.
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5050/api',
});

// Interceptor: Antes de cada requisiÃ§Ã£o, cola o token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;