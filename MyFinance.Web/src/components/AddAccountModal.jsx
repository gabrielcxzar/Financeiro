import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Radio, message, Switch, Row, Col, InputNumber } from 'antd';
import api from '../services/api';

export default function AddAccountModal({ visible, onClose, onSuccess, accountToEdit }) {
  const [loading, setLoading] = useState(false);
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    if (visible) {
      if (accountToEdit) {
        form.setFieldsValue(accountToEdit);
        setIsCreditCard(accountToEdit.isCreditCard);
      } else {
        form.resetFields();
        setIsCreditCard(false);
      }
    }
  }, [visible, accountToEdit]);

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
        isCreditCard: isCreditCard,
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
    } catch (error) {
      message.error('Erro ao salvar conta');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal 
        title={accountToEdit ? "Editar Conta" : "Nova Carteira / CartÃ£o"} 
        open={visible} 
        onOk={handleOk} 
        onCancel={onClose} 
        confirmLoading={loading}
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Checking', initialBalance: 0 }}>
        
        <Form.Item label="Ã‰ CartÃ£o de CrÃ©dito?">
          <Switch checked={isCreditCard} onChange={setIsCreditCard} checkedChildren="Sim" unCheckedChildren="NÃ£o" />
        </Form.Item>

        <Form.Item name="name" label={isCreditCard ? "Apelido do CartÃ£o" : "Nome da Conta"} rules={[{ required: true }]}>
          <Input placeholder={isCreditCard ? "Ex: Nubank Platinum" : "Ex: Carteira, Banco..."} />
        </Form.Item>

        {!accountToEdit && !isCreditCard && (
           <Form.Item name="initialBalance" label="Saldo Inicial">
              <InputNumber 
                  style={{ width: '100%' }} 
                  prefix="R$" 
                  decimalSeparator="," 
                  precision={2}
                  stringMode
              />
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
            <Form.Item name="creditLimit" label="Limite do CartÃ£o" rules={[{ required: true }]}>
              <InputNumber 
                  style={{ width: '100%' }} 
                  prefix="R$" 
                  decimalSeparator="," 
                  precision={2}
                  stringMode
              />
            </Form.Item>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item name="closingDay" label="Dia Fechamento" rules={[{ required: true }]}>
                  <Input type="number" min={1} max={31} />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item name="dueDay" label="Dia Vencimento" rules={[{ required: true }]}>
                  <Input type="number" min={1} max={31} />
                </Form.Item>
              </Col>
            </Row>
          </>
        )}
      </Form>
    </Modal>
  );
}