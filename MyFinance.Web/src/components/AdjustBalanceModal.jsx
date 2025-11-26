import React, { useState } from 'react';
import { Modal, Form, InputNumber, message } from 'antd';
import api from '../services/api';

export default function AdjustBalanceModal({ visible, onClose, onSuccess, account }) {
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);
      
      await api.post('/accounts/adjust-balance', {
        accountId: account.id,
        newBalance: Number(values.newBalance)
      });

      message.success('Saldo corrigido!');
      onSuccess();
      onClose();
    } catch (error) {
      message.error('Erro ao ajustar saldo');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={`Ajustar Saldo: ${account?.name}`}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
    >
      <p>Informe o valor exato que está no seu banco agora. O sistema criará um ajuste automático.</p>
      <Form form={form} layout="vertical">
        <Form.Item name="newBalance" label="Saldo Real Atual" rules={[{ required: true }]}>
          <InputNumber 
            style={{ width: '100%' }} 
            prefix="R$" 
            decimalSeparator="," 
            precision={2} 
            autoFocus
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}