import axios from 'axios';

const authExpiredEvent = 'finflow:auth-expired';
const persistentStorage = window.localStorage;
const sessionStorageRef = window.sessionStorage;

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

const markRequestStart = (config) => {
  if (typeof performance === 'undefined') return;

  const requestId = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  config.metadata = { requestId, startedAt: performance.now() };
  performance.mark(`finflow:api:start:${requestId}`);
};

const measureRequest = (config) => {
  const metadata = config?.metadata;
  if (typeof performance === 'undefined' || !metadata) return;

  const startMark = `finflow:api:start:${metadata.requestId}`;
  const method = (config.method || 'get').toUpperCase();
  const url = config.url || '';

  try {
    performance.measure(`finflow:api:${method}:${url}`, { start: startMark });
    performance.clearMarks(startMark);
  } catch {
    // Timing instrumentation must never affect API behavior.
  }
};

const getStoredAuthToken = () =>
  persistentStorage.getItem('token') || sessionStorageRef.getItem('token');

const storeAuthSession = ({ token, userName, rememberMe }) => {
  const target = rememberMe ? persistentStorage : sessionStorageRef;
  const alternate = rememberMe ? sessionStorageRef : persistentStorage;

  alternate.removeItem('token');
  alternate.removeItem('userName');
  target.setItem('token', token);
  target.setItem('userName', userName);
};

const clearStoredAuth = () => {
  persistentStorage.removeItem('token');
  persistentStorage.removeItem('userName');
  sessionStorageRef.removeItem('token');
  sessionStorageRef.removeItem('userName');
};

// Interceptor: Antes de cada requisicao, cola o token
api.interceptors.request.use((config) => {
  markRequestStart(config);
  const token = getStoredAuthToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => {
    measureRequest(response.config);
    return response;
  },
  (error) => {
    measureRequest(error.config);
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
      clearStoredAuth();
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

export { clearStoredAuth, getStoredAuthToken, storeAuthSession };
export { authExpiredEvent };
export default api;
