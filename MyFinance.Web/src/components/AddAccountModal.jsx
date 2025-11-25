import React, { useState } from 'react';
import { Modal, Form, Input, Radio, InputNumber, message, Switch, Divider, Row, Col } from 'antd';
import api from '../services/api';

export default function AddAccountModal({ visible, onClose, onSuccess }) {
  const [loading, setLoading] = useState(false);
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [form] = Form.useForm();

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);
      
      await api.post('/accounts', {
        name: values.name,
        initialBalance: Number(values.initialBalance || 0),
        currentBalance: Number(values.initialBalance || 0),
        type: isCreditCard ? 'CreditCard' : values.type,
        isCreditCard: isCreditCard,
        creditLimit: isCreditCard ? Number(values.creditLimit) : null,
        closingDay: isCreditCard ? Number(values.closingDay) : null,
        dueDay: isCreditCard ? Number(values.dueDay) : null,
      });

      message.success('Conta/Cartão salvo com sucesso!');
      form.resetFields();
      setIsCreditCard(false);
      onSuccess();
      onClose();
    } catch (error) {
      message.error('Erro ao criar conta');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title="Nova Carteira / Cartão" open={visible} onOk={handleOk} onCancel={onClose} confirmLoading={loading}>
      <Form form={form} layout="vertical" initialValues={{ type: 'Checking', initialBalance: 0 }}>
        
        <Form.Item label="É Cartão de Crédito?">
          <Switch checked={isCreditCard} onChange={setIsCreditCard} checkedChildren="Sim" unCheckedChildren="Não" />
        </Form.Item>

        <Form.Item name="name" label={isCreditCard ? "Apelido do Cartão" : "Nome da Conta"} rules={[{ required: true }]}>
          <Input placeholder={isCreditCard ? "Ex: Nubank Platinum" : "Ex: Carteira, Banco..."} />
        </Form.Item>

        {!isCreditCard ? (
          <>
            <Form.Item name="initialBalance" label="Saldo Atual" rules={[{ required: true }]}>
              <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
            </Form.Item>

            <Form.Item name="type" label="Tipo">
              <Radio.Group buttonStyle="solid">
                <Radio.Button value="Checking">Conta Corrente</Radio.Button>
                <Radio.Button value="Investment">Investimento</Radio.Button>
              </Radio.Group>
            </Form.Item>
          </>
        ) : (
          <>
            <Form.Item name="creditLimit" label="Limite do Cartão" rules={[{ required: true }]}>
              <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} placeholder="Ex: 5000,00" />
            </Form.Item>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item name="closingDay" label="Dia Fechamento" rules={[{ required: true }]}>
                  <InputNumber min={1} max={31} style={{ width: '100%' }} placeholder="Ex: 25" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item name="dueDay" label="Dia Vencimento" rules={[{ required: true }]}>
                  <InputNumber min={1} max={31} style={{ width: '100%' }} placeholder="Ex: 05" />
                </Form.Item>
              </Col>
            </Row>
            <div style={{ color: '#888', fontSize: '12px', marginTop: -10 }}>
              * Transações feitas após o dia do fechamento cairão no mês seguinte.
            </div>
          </>
        )}
      </Form>
    </Modal>
  );
}