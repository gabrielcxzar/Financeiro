import React, { useEffect, useState } from 'react';
import { Alert, Button, Space } from 'antd';
import { useRegisterSW } from 'virtual:pwa-register/react';

export default function PwaStatus() {
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);
  const [installPrompt, setInstallPrompt] = useState(null);
  const [installDismissed, setInstallDismissed] = useState(false);
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW();

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);
    const handleInstallPrompt = (event) => {
      event.preventDefault();
      setInstallPrompt(event);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    window.addEventListener('beforeinstallprompt', handleInstallPrompt);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      window.removeEventListener('beforeinstallprompt', handleInstallPrompt);
    };
  }, []);

  const install = async () => {
    if (!installPrompt) return;
    await installPrompt.prompt();
    setInstallPrompt(null);
  };

  if (isOnline && !needRefresh && (!installPrompt || installDismissed)) return null;

  return (
    <div className="pwa-status" role="status">
      {!isOnline && (
        <Alert
          type="warning"
          showIcon
          message="Você está sem conexão."
          description="Conecte-se à internet para carregar seus dados financeiros."
        />
      )}
      {needRefresh && (
        <Alert
          type="info"
          showIcon
          message="Nova versão do FinFlow disponível."
          action={<Button size="small" type="primary" onClick={() => updateServiceWorker(true)}>Atualizar</Button>}
          closable
          onClose={() => setNeedRefresh(false)}
        />
      )}
      {isOnline && !needRefresh && installPrompt && !installDismissed && (
        <Alert
          type="info"
          showIcon
          message="Instale o FinFlow para abrir como aplicativo."
          action={(
            <Space>
              <Button size="small" type="primary" onClick={install}>Instalar</Button>
              <Button size="small" type="text" onClick={() => setInstallDismissed(true)}>Agora não</Button>
            </Space>
          )}
        />
      )}
    </div>
  );
}
