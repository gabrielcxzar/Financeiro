import React, { useState, useEffect } from 'react';
import { Modal, Form, Select, message, InputNumber, DatePicker } from 'antd';
import { SwapOutlined } from '@ant-design/icons';
import api from '../services/api';
import dayjs from 'dayjs';

const { Option } = Select;

export default function TransferModal({ visible, onClose, onSuccess }) {
  const [form] = Form.useForm();
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (visible) {
      loadAccounts();
      form.resetFields();
    }
  }, [visible]);

  const loadAccounts = async () => {
    try {
      const response = await api.get('/accounts');
      setAccounts(response.data);
    } catch (error) {
      message.error('Erro ao buscar contas');
    }
  };

  const handleOk = () => {
    form.validateFields().then(async (values) => {
      if (values.fromAccountId === values.toAccountId) {
        return message.error('A conta de origem e destino devem ser diferentes.');
      }

      setLoading(true);
      try {
        await api.post('/transactions/transfer', {
          fromAccountId: values.fromAccountId,
          toAccountId: values.toAccountId,
          amount: Number(values.amount),
          date: values.date.toISOString()
        });

        message.success('Transferncia realizada!');
        onSuccess();
        onClose();
      } catch (error) {
        message.error('Erro na transferncia');
      } finally {
        setLoading(false);
      }
    });
  };

  const fromAccounts = accounts.filter(a => !a.isCreditCard);
  const toAccounts = accounts;

  return (
    <Modal
      title={<><SwapOutlined /> Nova Transferncia</>}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Transferir"
    >
      <Form form={form} layout="vertical">

        <Form.Item name="date" label="Data" initialValue={dayjs()}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="amount" label="Valor" rules={[{ required: true }]}>
          <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
        </Form.Item>

        <div style={{ display: 'flex', gap: 16 }}>
          <Form.Item name="fromAccountId" label="De (Origem)" style={{ flex: 1 }} rules={[{ required: true }]}>
            <Select placeholder="Sai de...">
              {fromAccounts.map(a => <Option key={a.id} value={a.id}>{a.name}</Option>)}
            </Select>
          </Form.Item>

          <Form.Item name="toAccountId" label="Para (Destino)" style={{ flex: 1 }} rules={[{ required: true }]}>
            <Select placeholder="Vai para...">
              {toAccounts.map(a => <Option key={a.id} value={a.id}>{a.name}</Option>)}
            </Select>
          </Form.Item>
        </div>

      </Form>
    </Modal>
  );
}
