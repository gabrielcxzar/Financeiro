import axios from 'axios';

const api = axios.create({
  // Se tiver VITE_API_URL, usa ela. Senao, usa localhost.
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5050/api',
  timeout: 20000,
});

// Interceptor: Antes de cada requisicao, cola o token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.code === 'ECONNABORTED' || error.message?.toLowerCase().includes('timeout')) {
      return Promise.reject(new Error('Timeout de conexao com a API. Tente novamente.'));
    }

    return Promise.reject(error);
  },
);

export default api;
