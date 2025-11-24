import React, { useState, useEffect } from 'react';
import { Popup, Form, Input, Button, Radio, Selector, Toast } from 'antd-mobile';
import api from '../services/api';

export default function AddTransactionModal({ visible, onClose, onSuccess }) {
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);

  // Busca Categorias e Contas assim que abre a janela
  useEffect(() => {
    if (visible) {
      loadData();
    }
  }, [visible]);

  const loadData = async () => {
    try {
      const [catResponse, accResponse] = await Promise.all([
        api.get('/categories'),
        api.get('/accounts')
      ]);
      setCategories(catResponse.data);
      setAccounts(accResponse.data);
    } catch (error) {
      Toast.show('Erro ao carregar dados');
    }
  };

  const onFinish = async (values) => {
    setLoading(true);
    try {
      // Prepara o objeto para enviar pro C#
      const transaction = {
        description: values.description,
        amount: parseFloat(values.amount),
        type: values.type, // Income ou Expense
        categoryId: values.categoryId[0], // O Selector devolve array
        accountId: values.accountId[0],
        date: new Date().toISOString(), // Data de hoje
        paid: true
      };

      // Se for Despesa, garantimos que o valor no banco seja negativo (opcional, mas bom pra lógica)
      // Mas o Mobills geralmente salva positivo e usa o TYPE para saber. 
      // Vamos manter positivo e o Front decide a cor.

      await api.post('/transactions', transaction);
      
      Toast.show({
        icon: 'success',
        content: 'Salvo com sucesso!',
      });
      
      onSuccess(); // Avisa a Home para atualizar o saldo
      onClose();   // Fecha a janelinha
    } catch (error) {
      console.error(error);
      Toast.show({ icon: 'fail', content: 'Erro ao salvar' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Popup
      visible={visible}
      onMaskClick={onClose}
      bodyStyle={{ borderTopLeftRadius: '20px', borderTopRightRadius: '20px', minHeight: '60vh' }}
    >
      <div style={{ padding: '20px' }}>
        <h2 style={{ marginTop: 0 }}>Nova Transação</h2>
        
        <Form 
            layout='horizontal' 
            onFinish={onFinish}
            footer={
              <Button block type='submit' color='primary' loading={loading} size='large'>
                Salvar
              </Button>
            }
        >
          <Form.Item name='type' initialValue='Expense'>
            <Radio.Group>
              <div style={{ display: 'flex', gap: 20 }}>
                <Radio value='Income' style={{ '--icon-color': 'green' }}>Receita</Radio>
                <Radio value='Expense' style={{ '--icon-color': 'red' }}>Despesa</Radio>
              </div>
            </Radio.Group>
          </Form.Item>

          <Form.Item name='amount' label='Valor' rules={[{ required: true }]}>
            <Input placeholder='0,00' type='number' />
          </Form.Item>

          <Form.Item name='description' label='Descrição' rules={[{ required: true }]}>
            <Input placeholder='Ex: Almoço' />
          </Form.Item>

          <Form.Item name='categoryId' label='Categoria' rules={[{ required: true, message: 'Selecione uma categoria' }]}>
            <Selector
              columns={2}
              // Correção: Tenta ler minúsculo OU maiúsculo
              options={categories.map(c => ({ 
                  label: c.name || c.Name, 
                  value: c.id || c.Id 
              }))}
            />
          </Form.Item>

          <Form.Item name='accountId' label='Conta' rules={[{ required: true, message: 'Selecione uma conta' }]}>
            <Selector
              columns={2}
              // Correção: Tenta ler minúsculo OU maiúsculo
              options={accounts.map(a => ({ 
                  label: a.name || a.Name, 
                  value: a.id || a.Id 
              }))}
            />
          </Form.Item>
        </Form>
      </div>
    </Popup>
  );
}