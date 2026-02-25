import React, { useState } from 'react';
import { Modal, Form, InputNumber, message, Grid } from 'antd';
import api from '../services/api';

const { useBreakpoint } = Grid;

export default function AdjustBalanceModal({ visible, onClose, onSuccess, account }) {
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const screens = useBreakpoint();
  const isCompact = !screens.md;

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      await api.post('/accounts/adjust-balance', {
        accountId: account.id,
        newBalance: Number(values.newBalance),
      });

      message.success('Saldo corrigido!');
      onSuccess();
      onClose();
      form.resetFields();
    } catch {
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
      width={isCompact ? 'calc(100vw - 20px)' : 460}
      destroyOnClose
    >
      <p>Informe o valor exato que esta no seu banco agora.</p>
      <Form form={form} layout="vertical">
        <Form.Item name="newBalance" label="Saldo Real Atual" rules={[{ required: true }]}>
          <InputNumber style={{ width: '100%' }} prefix="R$" decimalSeparator="," precision={2} autoFocus />
        </Form.Item>
      </Form>
    </Modal>
  );
}
