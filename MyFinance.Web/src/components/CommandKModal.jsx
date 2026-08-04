import React, { useState, useEffect } from 'react';
import { Modal, Input, List, Tag } from 'antd';
import {
  SearchOutlined,
  HomeOutlined,
  UnorderedListOutlined,
  BankOutlined,
  TagsOutlined,
  TrophyOutlined,
  PieChartOutlined,
  PlusCircleOutlined,
  StarOutlined,
  EyeOutlined,
  CreditCardOutlined,
  EnterOutlined,
} from '@ant-design/icons';

export default function CommandKModal({ open, onClose, onNavigate, onAddTransaction, onOpenOnboarding, onToggleVisibility }) {
  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);

  const commandItems = [
    {
      id: 'dash',
      title: 'Ir para Dashboard',
      category: 'Navegação',
      icon: <HomeOutlined style={{ color: '#FF6600' }} />,
      action: () => { onNavigate('1'); onClose(); },
    },
    {
      id: 'add',
      title: 'Nova Transação',
      category: 'Ações Rápidas',
      icon: <PlusCircleOutlined style={{ color: '#10B981' }} />,
      shortcut: 'N',
      action: () => { onAddTransaction(); onClose(); },
    },
    {
      id: 'trans',
      title: 'Ver Extrato de Transações',
      category: 'Navegação',
      icon: <UnorderedListOutlined style={{ color: '#3B82F6' }} />,
      action: () => { onNavigate('2'); onClose(); },
    },
    {
      id: 'acc',
      title: 'Gerenciar Contas e Carteiras',
      category: 'Navegação',
      icon: <BankOutlined style={{ color: '#8B5CF6' }} />,
      action: () => { onNavigate('4'); onClose(); },
    },
    {
      id: 'cards',
      title: 'Ver Faturas de Cartão',
      category: 'Navegação',
      icon: <CreditCardOutlined style={{ color: '#EC4899' }} />,
      action: () => { onNavigate('8'); onClose(); },
    },
    {
      id: 'goals',
      title: 'Ver Metas Financeiras',
      category: 'Navegação',
      icon: <TrophyOutlined style={{ color: '#F59E0B' }} />,
      action: () => { onNavigate('7'); onClose(); },
    },
    {
      id: 'cats',
      title: 'Gerenciar Categorias',
      category: 'Navegação',
      icon: <TagsOutlined style={{ color: '#10B981' }} />,
      action: () => { onNavigate('6'); onClose(); },
    },
    {
      id: 'reports',
      title: 'Abrir Relatórios & Gráficos',
      category: 'Navegação',
      icon: <PieChartOutlined style={{ color: '#06B6D4' }} />,
      action: () => { onNavigate('3'); onClose(); },
    },
    {
      id: 'onboard',
      title: 'Abrir Guia de Primeiros Passos',
      category: 'Ajuda',
      icon: <StarOutlined style={{ color: '#FF6600' }} />,
      action: () => { onOpenOnboarding(); onClose(); },
    },
    {
      id: 'toggle_vis',
      title: 'Ocultar / Mostrar Valores',
      category: 'Preferências',
      icon: <EyeOutlined style={{ color: '#64748B' }} />,
      action: () => { onToggleVisibility(); onClose(); },
    },
  ];

  const filteredItems = commandItems.filter(
    (item) =>
      item.title.toLowerCase().includes(query.toLowerCase()) ||
      item.category.toLowerCase().includes(query.toLowerCase())
  );

  useEffect(() => {
    setSelectedIndex(0);
  }, [query]);

  const handleKeyDown = (e) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % (filteredItems.length || 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelectedIndex((prev) => (prev - 1 + filteredItems.length) % (filteredItems.length || 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (filteredItems[selectedIndex]) {
        filteredItems[selectedIndex].action();
      }
    }
  };

  return (
    <Modal
      open={open}
      footer={null}
      onCancel={onClose}
      width={560}
      centered
      destroyOnClose
      styles={{ body: { padding: 0 } }}
      style={{ borderRadius: 16, overflow: 'hidden' }}
    >
      <div style={{ padding: '16px 20px', borderBottom: '1px solid #E2E8F0', display: 'flex', alignItems: 'center', gap: 12 }}>
        <SearchOutlined style={{ fontSize: 20, color: '#FF6600' }} />
        <Input
          placeholder="Digite um comando ou navegue... (Ex: transações, conta, ocultar)"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          autoFocus
          bordered={false}
          style={{ fontSize: 15, width: '100%' }}
        />
        <Tag style={{ borderRadius: 6, margin: 0, fontWeight: 700 }}>ESC</Tag>
      </div>

      <div style={{ maxHeight: 360, overflowY: 'auto', padding: '8px 12px' }}>
        <List
          dataSource={filteredItems}
          renderItem={(item, idx) => {
            const isSelected = idx === selectedIndex;
            return (
              <List.Item
                onClick={item.action}
                onMouseEnter={() => setSelectedIndex(idx)}
                style={{
                  padding: '10px 14px',
                  borderRadius: 10,
                  cursor: 'pointer',
                  border: 'none',
                  background: isSelected ? 'rgba(255, 102, 0, 0.08)' : 'transparent',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  transition: 'background 0.15s ease',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <div style={{ fontSize: 18 }}>{item.icon}</div>
                  <div>
                    <strong style={{ display: 'block', fontSize: 14, color: isSelected ? '#FF6600' : '#0F172A' }}>
                      {item.title}
                    </strong>
                    <span style={{ fontSize: 11, color: '#64748B' }}>{item.category}</span>
                  </div>
                </div>

                {isSelected && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#FF6600', fontSize: 12 }}>
                    <span>Executar</span>
                    <EnterOutlined />
                  </div>
                )}
              </List.Item>
            );
          }}
        />
      </div>

      <div style={{ padding: '10px 16px', background: '#F8FAFC', borderTop: '1px solid #E2E8F0', display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#64748B' }}>
        <span>Use <strong style={{ color: '#0F172A' }}>↑ ↓</strong> para navegar</span>
        <span><strong style={{ color: '#0F172A' }}>Enter</strong> para selecionar</span>
      </div>
    </Modal>
  );
}
