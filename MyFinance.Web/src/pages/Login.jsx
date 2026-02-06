import React, { useState } from 'react';
import { Card, Form, Input, Button, Typography, message, Tabs } from 'antd';
import { UserOutlined, LockOutlined, MailOutlined } from '@ant-design/icons';
import api from '../services/api';

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
      message.error('Email ou senha incorretos');
    } finally {
      setLoading(false);
    }
  };

  const onFinishRegister = async (values) => {
    setLoading(true);
    try {
      await api.post('/auth/register', values);
      message.success('Cadastro realizado! FaÃ§a login agora.');
    } catch (error) {
      message.error('Erro ao cadastrar. Tente outro email.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: '#f0f2f5' }}>
      <Card style={{ width: 400, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
        <div style={{ textAlign: 'center', marginBottom: 20 }}>
            <Title level={2} style={{ color: '#1890ff' }}>MyFinance</Title>
            <p>Controle financeiro familiar</p>
        </div>

        <Tabs defaultActiveKey="1" items={[
            {
                key: '1', label: 'Entrar', children: (
                    <Form onFinish={onFinishLogin} layout="vertical">
                        <Form.Item name="email" rules={[{ required: true, message: 'Insira seu email' }]}>
                            <Input prefix={<UserOutlined />} placeholder="Email" size="large" autoComplete="email"/>
                        </Form.Item>
                        <Form.Item name="password" rules={[{ required: true, message: 'Insira sua senha' }]}>
                            <Input.Password prefix={<LockOutlined />} placeholder="Senha" size="large" autoComplete="current-password"/>
                        </Form.Item>
                        <Button type="primary" htmlType="submit" block size="large" loading={loading}>Entrar</Button>
                    </Form>
                )
            },
            {
                key: '2', label: 'Cadastrar', children: (
                    <Form onFinish={onFinishRegister} layout="vertical">
                        <Form.Item name="name" rules={[{ required: true, message: 'Seu nome' }]}>
                            <Input prefix={<UserOutlined />} placeholder="Nome" autoComplete="name"/>
                        </Form.Item>
                        <Form.Item name="email" rules={[{ required: true, message: 'Seu email' }]}>
                            <Input prefix={<MailOutlined />} placeholder="Email" autoComplete="email"/>
                        </Form.Item>
                        <Form.Item name="password" rules={[{ required: true, message: 'Crie uma senha' }]}>
                            <Input.Password prefix={<LockOutlined />} placeholder="Senha" autoComplete="new-password"/>
                        </Form.Item>
                        <Button htmlType="submit" block loading={loading}>Criar Conta</Button>
                    </Form>
                )
            }
        ]} />
      </Card>
    </div>
  );
}