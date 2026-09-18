import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import ptBR from 'antd/locale/pt_BR';
import App from './App.jsx';
import './index.css';
import PwaStatus from './components/PwaStatus';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ConfigProvider locale={ptBR}>
      <PwaStatus />
      <App />
    </ConfigProvider>
  </React.StrictMode>,
);
