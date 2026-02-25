import React, { useState } from 'react';
import { Card, Form, Input, Button, Typography, message, Tabs } from 'antd';
import { UserOutlined, LockOutlined, MailOutlined } from '@ant-design/icons';
import api from '../services/api';
import './Login.css';

const { Title } = Typography;

export default function Login({ onLoginSuccess }) {
  const [loading, setLoading] = useState(false);

  const onFinishLogin = async (values) => {
    setLoading(true);
    try {
      const { data } = await api.post('/auth/login', values);
      localStorage.setItem('token', data.token);
      localStorage.setItem('userName', data.name);
      message.success(`Bem-vindo, ${data.name}!`);
      onLoginSuccess();
    } catch (error) {
      message.error('Email ou senha incorretos.');
    } finally {
      setLoading(false);
    }
  };

  const onFinishRegister = async (values) => {
    setLoading(true);
    try {
      await api.post('/auth/register', values);
      message.success('Cadastro realizado! Faça login agora.');
    } catch (error) {
      message.error('Erro ao cadastrar. Tente outro email.');
    } finally {
      setLoading(false);
    }
  };

  const tabItems = [
    {
      key: '1',
      label: 'Entrar',
      children: (
        <Form onFinish={onFinishLogin} layout="vertical" className="login-form">
          <Form.Item name="email" rules={[{ required: true, message: 'Insira seu email.' }]}>
            <Input prefix={<UserOutlined />} placeholder="Email" size="large" autoComplete="email" />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: 'Insira sua senha.' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Senha" size="large" autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block size="large" loading={loading} className="login-submit">
            Entrar
          </Button>
        </Form>
      )
    },
    {
      key: '2',
      label: 'Cadastrar',
      children: (
        <Form onFinish={onFinishRegister} layout="vertical" className="login-form">
          <Form.Item name="name" rules={[{ required: true, message: 'Digite seu nome.' }]}>
            <Input prefix={<UserOutlined />} placeholder="Nome" size="large" autoComplete="name" />
          </Form.Item>
          <Form.Item name="email" rules={[{ required: true, message: 'Digite seu email.' }]}>
            <Input prefix={<MailOutlined />} placeholder="Email" size="large" autoComplete="email" />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: 'Crie uma senha.' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Senha" size="large" autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block size="large" loading={loading} className="login-submit">
            Criar conta
          </Button>
        </Form>
      )
    }
  ];

  return (
    <div className="login-shell">
      <div className="login-layout">
        <aside className="login-brand-panel">
          <img src="/brand-mark.svg" alt="MyFinance" className="login-brand-logo" />
          <h1>MyFinance</h1>
          <p>
            Gestão financeira com visão de fluxo mensal, recorrências e metas em um único painel.
          </p>
          <ul className="login-brand-list">
            <li>Projeção dos próximos meses</li>
            <li>Controle de contas e cartões</li>
            <li>Relatórios para decisões rápidas</li>
          </ul>
        </aside>

        <section className="login-form-panel">
          <Card className="login-card" bordered={false}>
            <div className="login-card-header">
              <img src="/brand-mark.svg" alt="" aria-hidden="true" className="login-card-logo" />
              <div>
                <Title level={2} className="login-title">Acesse sua conta</Title>
                <p className="login-subtitle">Continue de onde parou no seu planejamento.</p>
              </div>
            </div>
            <Tabs defaultActiveKey="1" items={tabItems} className="login-tabs" />
          </Card>
        </section>
      </div>
    </div>
  );
}
