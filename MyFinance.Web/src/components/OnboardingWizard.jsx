import React, { useState, useEffect } from 'react';
import { Modal, Steps, Button, Form, Input, InputNumber, Radio, message, Tag, Card, Row, Col, Space, Progress } from 'antd';
import {
  RocketOutlined,
  CheckCircleOutlined,
  BankOutlined,
  TagsOutlined,
  DollarOutlined,
  ArrowRightOutlined,
  StarOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons';
import api from '../services/api';

const DEFAULT_CATEGORIES = [
  { name: 'Salário & Renda', type: 'Income', color: '#10B981' },
  { name: 'Investimentos & Rendimentos', type: 'Income', color: '#3B82F6' },
  { name: 'Alimentação & Mercado', type: 'Expense', color: '#EF4444' },
  { name: 'Moradia & Contas', type: 'Expense', color: '#F59E0B' },
  { name: 'Transporte & Combustível', type: 'Expense', color: '#8B5CF6' },
  { name: 'Lazer & Entretenimento', type: 'Expense', color: '#EC4899' },
  { name: 'Saúde & Bem-estar', type: 'Expense', color: '#06B6D4' },
  { name: 'Assinaturas & Serviços', type: 'Expense', color: '#6366F1' },
];

export default function OnboardingWizard({ open, onClose, onComplete }) {
  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [categoriesImported, setCategoriesImported] = useState(false);
  const [accountCreated, setAccountCreated] = useState(false);
  const [createdAccountId, setCreatedAccountId] = useState(null);

  const [accountForm] = Form.useForm();

  useEffect(() => {
    if (open) {
      setCurrentStep(0);
    }
  }, [open]);

  // Handle Import Default Categories
  const handleImportCategories = async () => {
    setLoading(true);
    try {
      // Get existing categories to avoid duplicates
      const { data: existing } = await api.get('/categories');
      const existingNames = new Set(existing.map((c) => c.name.toLowerCase()));

      let count = 0;
      for (const cat of DEFAULT_CATEGORIES) {
        if (!existingNames.has(cat.name.toLowerCase())) {
          await api.post('/categories', cat);
          count++;
        }
      }

      setCategoriesImported(true);
      message.success(`${count > 0 ? `${count} categorias` : 'Categorias'} prontas para uso!`);
      setCurrentStep(2);
    } catch (err) {
      console.error(err);
      message.error('Não foi possível importar as categorias automáticas.');
    } finally {
      setLoading(false);
    }
  };

  // Handle Create Account
  const handleCreateAccount = async () => {
    try {
      const values = await accountForm.validateFields();
      setLoading(true);

      const payload = {
        name: values.name,
        type: values.type || 'Checking',
        initialBalance: Number(values.initialBalance || 0),
        currentBalance: Number(values.initialBalance || 0),
        isCreditCard: false,
      };

      const response = await api.post('/accounts', payload);
      if (response.data && response.data.id) {
        setCreatedAccountId(response.data.id);
      }
      setAccountCreated(true);
      message.success('Sua primeira conta foi criada com sucesso!');
      setCurrentStep(3);
    } catch (err) {
      if (err?.errorFields) return; // Validation error
      message.error('Erro ao criar conta.');
    } finally {
      setLoading(false);
    }
  };

  const handleFinish = () => {
    localStorage.setItem('finflow_onboarding_completed', 'true');
    onComplete();
    onClose();
  };

  const stepsItems = [
    { title: 'Início', icon: <RocketOutlined /> },
    { title: 'Categorias', icon: <TagsOutlined /> },
    { title: 'Conta', icon: <BankOutlined /> },
    { title: 'Pronto!', icon: <CheckCircleOutlined /> },
  ];

  return (
    <Modal
      open={open}
      footer={null}
      onCancel={onClose}
      width={680}
      centered
      destroyOnClose
      maskClosable={false}
      bodyStyle={{ padding: '28px 32px' }}
      style={{ borderRadius: 20, overflow: 'hidden' }}
    >
      <div style={{ marginBottom: 24, textAlign: 'center' }}>
        <div
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 8,
            padding: '4px 14px',
            borderRadius: 99,
            background: 'rgba(255, 102, 0, 0.1)',
            color: '#FF6600',
            fontWeight: 700,
            fontSize: 13,
            marginBottom: 10,
          }}
        >
          <StarOutlined /> GUIA DE PRIMEIROS PASSOS
        </div>
        <Steps current={currentStep} items={stepsItems} size="small" style={{ marginTop: 12 }} />
      </div>

      {/* STEP 0: WELCOME */}
      {currentStep === 0 && (
        <div style={{ textAlign: 'center', padding: '10px 0' }} className="animate-fade-in">
          <div
            style={{
              width: 80,
              height: 80,
              borderRadius: 24,
              background: 'linear-gradient(135deg, #181A20 0%, #0B0C10 100%)',
              border: '2px solid #FF6600',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 20px',
              boxShadow: '0 12px 30px rgba(255, 102, 0, 0.25)',
            }}
          >
            <img src="/brand-mark.svg" alt="Finflow" style={{ width: 48, height: 48 }} />
          </div>

          <h2 style={{ fontSize: 26, margin: '0 0 10px', color: '#0F172A' }}>
            Bem-vindo ao <span style={{ color: '#FF6600' }}>Finflow</span>! 👋
          </h2>
          <p style={{ color: '#64748B', fontSize: 15, maxWidth: 480, margin: '0 auto 24px', lineHeight: 1.6 }}>
            Vamos configurar sua plataforma em menos de 2 minutos para você ter total controle do seu dinheiro sem complicação.
          </p>

          <Row gutter={[16, 16]} style={{ marginBottom: 28, textAlign: 'left' }}>
            <Col span={12}>
              <Card size="small" bodyStyle={{ padding: 14 }}>
                <Space align="start">
                  <div style={{ color: '#FF6600', fontSize: 20 }}><BankOutlined /></div>
                  <div>
                    <strong style={{ display: 'block', fontSize: 14 }}>1. Cadastre suas Contas</strong>
                    <span style={{ fontSize: 12, color: '#64748B' }}>Contas bancárias, carteiras e cartões.</span>
                  </div>
                </Space>
              </Card>
            </Col>
            <Col span={12}>
              <Card size="small" bodyStyle={{ padding: 14 }}>
                <Space align="start">
                  <div style={{ color: '#10B981', fontSize: 20 }}><TagsOutlined /></div>
                  <div>
                    <strong style={{ display: 'block', fontSize: 14 }}>2. Organize por Categorias</strong>
                    <span style={{ fontSize: 12, color: '#64748B' }}>Alimentação, Moradia, Salário...</span>
                  </div>
                </Space>
              </Card>
            </Col>
          </Row>

          <Button
            type="primary"
            size="large"
            icon={<ArrowRightOutlined />}
            onClick={() => setCurrentStep(1)}
            style={{ height: 48, padding: '0 36px', fontSize: 16, borderRadius: 12 }}
          >
            Começar Configuração Guiada
          </Button>
        </div>
      )}

      {/* STEP 1: IMPORT CATEGORIES */}
      {currentStep === 1 && (
        <div className="animate-fade-in">
          <h3 style={{ fontSize: 20, margin: '0 0 6px', color: '#0F172A' }}>
            🏷️ 1. Categorias Essenciais
          </h3>
          <p style={{ color: '#64748B', fontSize: 14, margin: '0 0 18px' }}>
            As categorias ajudam a entender para onde seu dinheiro vai. Quer que criemos um kit inicial completo para você com 1 clique?
          </p>

          <div
            style={{
              background: '#F8FAFC',
              border: '1px solid #E2E8F0',
              borderRadius: 14,
              padding: 16,
              marginBottom: 24,
            }}
          >
            <strong style={{ fontSize: 13, color: '#475569', display: 'block', marginBottom: 10 }}>
              Categorias sugeridas inclusas no kit:
            </strong>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
              {DEFAULT_CATEGORIES.map((cat, idx) => (
                <Tag key={idx} color={cat.type === 'Income' ? 'green' : 'volcano'} style={{ borderRadius: 6, padding: '4px 10px', fontSize: 12 }}>
                  {cat.name}
                </Tag>
              ))}
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Button type="text" onClick={() => setCurrentStep(2)}>
              Pular esta etapa
            </Button>
            <Button
              type="primary"
              size="large"
              loading={loading}
              onClick={handleImportCategories}
              style={{ borderRadius: 12, height: 44 }}
            >
              Importar Categorias (1-Clique)
            </Button>
          </div>
        </div>
      )}

      {/* STEP 2: CREATE FIRST ACCOUNT */}
      {currentStep === 2 && (
        <div className="animate-fade-in">
          <h3 style={{ fontSize: 20, margin: '0 0 6px', color: '#0F172A' }}>
            💳 2. Sua Primeira Conta ou Carteira
          </h3>
          <p style={{ color: '#64748B', fontSize: 14, margin: '0 0 20px' }}>
            Onde seu dinheiro está guardado? Pode ser sua conta no banco principal ou carteira física.
          </p>

          <Form form={accountForm} layout="vertical" initialValues={{ name: 'Conta Principal', initialBalance: 0, type: 'Checking' }}>
            <Form.Item
              name="name"
              label="Nome da Conta ou Banco"
              rules={[{ required: true, message: 'Informe o nome da conta' }]}
            >
              <Input size="large" placeholder="Ex: Itaú, Nubank, Carteira..." style={{ borderRadius: 10 }} />
            </Form.Item>

            <Row gutter={16}>
              <Col span={14}>
                <Form.Item name="initialBalance" label="Saldo Atual de Início">
                  <InputNumber
                    size="large"
                    prefix="R$"
                    style={{ width: '100%', borderRadius: 10 }}
                    decimalSeparator=","
                    precision={2}
                  />
                </Form.Item>
              </Col>
              <Col span={10}>
                <Form.Item name="type" label="Tipo de Conta">
                  <Radio.Group size="large" buttonStyle="solid">
                    <Radio.Button value="Checking">Corrente</Radio.Button>
                    <Radio.Button value="Investment">Investimento</Radio.Button>
                  </Radio.Group>
                </Form.Item>
              </Col>
            </Row>
          </Form>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12 }}>
            <Button type="text" onClick={() => setCurrentStep(1)}>
              Voltar
            </Button>
            <Button
              type="primary"
              size="large"
              loading={loading}
              onClick={handleCreateAccount}
              style={{ borderRadius: 12, height: 44 }}
            >
              Salvar Conta e Continuar
            </Button>
          </div>
        </div>
      )}

      {/* STEP 3: ALL DONE */}
      {currentStep === 3 && (
        <div style={{ textAlign: 'center', padding: '16px 0' }} className="animate-fade-in">
          <div
            style={{
              width: 72,
              height: 72,
              borderRadius: '50%',
              background: 'rgba(16, 185, 129, 0.12)',
              color: '#10B981',
              fontSize: 36,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
            }}
          >
            <CheckCircleOutlined />
          </div>

          <h2 style={{ fontSize: 24, margin: '0 0 8px', color: '#0F172A' }}>
            Tudo pronto! Seu Finflow está configurado. 🎉
          </h2>
          <p style={{ color: '#64748B', fontSize: 14, maxWidth: 440, margin: '0 auto 24px', lineHeight: 1.6 }}>
            Você já tem sua primeira conta criada e categorias prontas. Agora é só acompanhar seu saldo e lançar suas movimentações no dia a dia.
          </p>

          <Progress percent={100} strokeColor="#FF6600" style={{ maxWidth: 300, margin: '0 auto 28px' }} />

          <Button
            type="primary"
            size="large"
            onClick={handleFinish}
            style={{ height: 48, padding: '0 40px', fontSize: 16, borderRadius: 12 }}
          >
            Ir para o Meu Dashboard
          </Button>
        </div>
      )}
    </Modal>
  );
}
