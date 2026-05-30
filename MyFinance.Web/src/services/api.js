import axios from 'axios';

const authExpiredEvent = 'finflow:auth-expired';

const resolveBaseUrl = () => {
  const configured = import.meta.env.VITE_API_URL?.trim();
  if (configured) {
    return configured.replace(/\/+$/, '');
  }

  return 'http://localhost:10000/api';
};

const buildApiError = (error, message) => {
  const normalized = new Error(message);
  normalized.name = 'ApiError';
  normalized.code = error.code;
  normalized.status = error.response?.status;
  normalized.response = error.response;
  normalized.originalError = error;
  return normalized;
};

const api = axios.create({
  baseURL: resolveBaseUrl(),
  timeout: 60000,
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
    if (error.code === 'ERR_CANCELED') {
      return Promise.reject(error);
    }

    if (error.code === 'ECONNABORTED' || error.message?.toLowerCase().includes('timeout')) {
      return Promise.reject(buildApiError(error, 'Timeout de conexao com a API. Tente novamente.'));
    }

    if (!error.response) {
      return Promise.reject(buildApiError(error, 'Nao foi possivel conectar com a API. Verifique a conexao e tente novamente.'));
    }

    if (error.response.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('userName');
      window.dispatchEvent(new Event(authExpiredEvent));
      return Promise.reject(buildApiError(error, 'Sua sessao expirou. Entre novamente.'));
    }

    if (error.response.status === 403) {
      return Promise.reject(buildApiError(error, 'Voce nao tem permissao para executar essa acao.'));
    }

    if (error.response.status >= 500) {
      return Promise.reject(buildApiError(error, 'A API encontrou um erro interno. Tente novamente em instantes.'));
    }

    if (typeof error.response.data === 'string' && error.response.data.trim()) {
      return Promise.reject(buildApiError(error, error.response.data));
    }

    return Promise.reject(buildApiError(error, error.message || 'Erro inesperado ao comunicar com a API.'));
  },
);

export { authExpiredEvent };
export default api;
