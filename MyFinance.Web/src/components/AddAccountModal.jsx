import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Radio, message, Switch, Row, Col, InputNumber, Grid } from 'antd';
import api from '../services/api';

const { useBreakpoint } = Grid;

export default function AddAccountModal({ visible, onClose, onSuccess, accountToEdit }) {
  const [loading, setLoading] = useState(false);
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [form] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    if (!visible) return;

    if (accountToEdit) {
      form.setFieldsValue(accountToEdit);
      setIsCreditCard(accountToEdit.isCreditCard);
      return;
    }

    form.resetFields();
    form.setFieldsValue({ type: 'Checking', initialBalance: 0 });
    setIsCreditCard(false);
  }, [visible, accountToEdit, form]);

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const payload = {
        ...values,
        id: accountToEdit ? accountToEdit.id : 0,
        initialBalance: Number(values.initialBalance || 0),
        currentBalance: accountToEdit ? accountToEdit.currentBalance : Number(values.initialBalance || 0),
        type: isCreditCard ? 'Checking' : values.type,
        isCreditCard,
        creditLimit: Number(values.creditLimit || 0),
        closingDay: values.closingDay ? Number(values.closingDay) : null,
        dueDay: values.dueDay ? Number(values.dueDay) : null,
      };

      if (accountToEdit) {
        await api.put(`/accounts/${accountToEdit.id}`, payload);
        message.success('Conta atualizada!');
      } else {
        await api.post('/accounts', payload);
        message.success('Conta criada!');
      }

      onSuccess();
      onClose();
    } catch {
      message.error('Erro ao salvar conta');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={accountToEdit ? 'Editar Conta' : 'Nova Carteira / Cartao'}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      width={isCompact ? 'calc(100vw - 20px)' : 560}
      destroyOnClose
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Checking', initialBalance: 0 }}>
        <Form.Item label="E cartao de credito?">
          <Switch checked={isCreditCard} onChange={setIsCreditCard} checkedChildren="Sim" unCheckedChildren="Nao" />
        </Form.Item>

        <Form.Item
          name="name"
          label={isCreditCard ? 'Apelido do cartao' : 'Nome da conta'}
          rules={[{ required: true }]}
        >
          <Input placeholder={isCreditCard ? 'Ex: Nubank Platinum' : 'Ex: Carteira, Banco...'} />
        </Form.Item>

        {!accountToEdit && !isCreditCard && (
          <Form.Item name="initialBalance" label="Saldo Inicial">
            <InputNumber style={{ width: '100%' }} prefix="R$" decimalSeparator="," precision={2} stringMode />
          </Form.Item>
        )}

        {!isCreditCard && (
          <Form.Item name="type" label="Tipo">
            <Radio.Group buttonStyle="solid">
              <Radio.Button value="Checking">Conta Corrente</Radio.Button>
              <Radio.Button value="Investment">Investimento</Radio.Button>
            </Radio.Group>
          </Form.Item>
        )}

        {isCreditCard && (
          <>
            <Form.Item name="creditLimit" label="Limite do Cartao" rules={[{ required: true }]}>
              <InputNumber style={{ width: '100%' }} prefix="R$" decimalSeparator="," precision={2} stringMode />
            </Form.Item>

            <Row gutter={12}>
              <Col xs={24} sm={12}>
                <Form.Item name="closingDay" label="Dia Fechamento" rules={[{ required: true }]}>
                  <InputNumber min={1} max={31} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="dueDay" label="Dia Vencimento" rules={[{ required: true }]}>
                  <InputNumber min={1} max={31} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>
          </>
        )}
      </Form>
    </Modal>
  );
}
