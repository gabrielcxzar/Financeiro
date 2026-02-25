import React, { useState, useEffect } from 'react';
import { Modal, Form, Select, message, InputNumber, DatePicker, Row, Col, Grid } from 'antd';
import { SwapOutlined } from '@ant-design/icons';
import api from '../services/api';
import dayjs from 'dayjs';

const { Option } = Select;
const { useBreakpoint } = Grid;

export default function TransferModal({ visible, onClose, onSuccess }) {
  const [form] = Form.useForm();
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    if (visible) {
      loadAccounts();
      form.resetFields();
      form.setFieldsValue({ date: dayjs() });
    }
  }, [visible, form]);

  const loadAccounts = async () => {
    try {
      const response = await api.get('/accounts');
      setAccounts(response.data);
    } catch {
      message.error('Erro ao buscar contas');
    }
  };

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      if (values.fromAccountId === values.toAccountId) {
        return message.error('A conta de origem e destino devem ser diferentes.');
      }

      setLoading(true);
      await api.post('/transactions/transfer', {
        fromAccountId: values.fromAccountId,
        toAccountId: values.toAccountId,
        amount: Number(values.amount),
        date: values.date.toISOString(),
      });

      message.success('Transferencia realizada!');
      onSuccess();
      onClose();
    } catch {
      message.error('Erro na transferencia');
    } finally {
      setLoading(false);
    }
  };

  const fromAccounts = accounts.filter((a) => !a.isCreditCard);
  const toAccounts = accounts;

  return (
    <Modal
      title={
        <>
          <SwapOutlined /> Nova Transferencia
        </>
      }
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Transferir"
      width={isCompact ? 'calc(100vw - 20px)' : 560}
      destroyOnClose
    >
      <Form form={form} layout="vertical">
        <Form.Item name="date" label="Data" initialValue={dayjs()} rules={[{ required: true }]}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} allowClear={false} />
        </Form.Item>

        <Form.Item name="amount" label="Valor" rules={[{ required: true }]}>
          <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
        </Form.Item>

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="fromAccountId" label="De (Origem)" rules={[{ required: true }]}>
              <Select placeholder="Sai de..." showSearch optionFilterProp="children">
                {fromAccounts.map((a) => (
                  <Option key={a.id} value={a.id}>
                    {a.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>

          <Col xs={24} sm={12}>
            <Form.Item name="toAccountId" label="Para (Destino)" rules={[{ required: true }]}>
              <Select placeholder="Vai para..." showSearch optionFilterProp="children">
                {toAccounts.map((a) => (
                  <Option key={a.id} value={a.id}>
                    {a.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  );
}
