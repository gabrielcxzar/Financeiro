import axios from 'axios';

const api = axios.create({
    // Lembre-se: sua API C# está rodando na porta 5050 (conforme seu print anterior)
    baseURL: 'http://localhost:5050/api', 
});

export default api;