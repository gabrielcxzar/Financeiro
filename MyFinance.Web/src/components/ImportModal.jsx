import React, { useState, useEffect } from 'react';
import { Modal, Form, Select, Upload, Button, message, Divider } from 'antd';
import { InboxOutlined, BankOutlined, CreditCardOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Dragger } = Upload;
const { Option } = Select;

export default function ImportModal({ visible, onClose, onSuccess }) {
  const [accounts, setAccounts] = useState([]);
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [fileList, setFileList] = useState([]);
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    if (visible) {
      loadAccounts();
      setFileList([]);
      setSelectedAccount(null);
    }
  }, [visible]);

  const loadAccounts = async () => {
    try {
      const response = await api.get('/accounts');
      setAccounts(response.data);
    } catch (error) {
      message.error('Erro ao carregar contas');
    }
  };

  const handleUpload = async () => {
    if (!selectedAccount) return message.error('Selecione uma conta de destino!');
    if (fileList.length === 0) return message.error('Selecione um arquivo CSV!');

    const formData = new FormData();
    formData.append('file', fileList[0]);

    setUploading(true);
    try {
      // Chama a API passando o ID da conta na URL
      const response = await api.post(`/import/upload?accountId=${selectedAccount}`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      
      message.success(response.data.message);
      onSuccess(); // Atualiza a tela de fundo
      onClose();
    } catch (error) {
      console.error(error);
      message.error('Erro na importação. Verifique se o CSV é válido.');
    } finally {
      setUploading(false);
    }
  };

  const uploadProps = {
    onRemove: () => setFileList([]),
    beforeUpload: (file) => {
      const isCSV = file.type === 'text/csv' || file.name.endsWith('.csv');
      if (!isCSV) {
        message.error('Apenas arquivos CSV são permitidos!');
        return Upload.LIST_IGNORE;
      }
      setFileList([file]);
      return false; // Impede upload automático (espera o botão)
    },
    fileList,
  };

  return (
    <Modal
      title="Importar Extrato / Fatura (CSV)"
      open={visible}
      onCancel={onClose}
      footer={[
        <Button key="back" onClick={onClose}>Cancelar</Button>,
        <Button key="submit" type="primary" loading={uploading} onClick={handleUpload} disabled={fileList.length === 0}>
          Processar Arquivo
        </Button>,
      ]}
    >
      <div style={{ marginBottom: 16 }}>
        <label style={{ display: 'block', marginBottom: 8 }}>Para qual conta esses dados vão?</label>
        <Select 
            style={{ width: '100%' }} 
            placeholder="Selecione a Conta ou Cartão"
            onChange={setSelectedAccount}
        >
            {accounts.map(acc => (
                <Option key={acc.id} value={acc.id}>
                    {acc.isCreditCard ? <CreditCardOutlined /> : <BankOutlined />} {acc.name}
                </Option>
            ))}
        </Select>
      </div>

      <Dragger {...uploadProps} style={{ padding: 20 }}>
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">Clique ou arraste o arquivo CSV aqui</p>
        <p className="ant-upload-hint">
          Suporta arquivos exportados do Nubank (Extrato ou Fatura).
        </p>
      </Dragger>
    </Modal>
  );
}