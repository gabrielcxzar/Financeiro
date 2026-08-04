import React from 'react';
import { Button, Card } from 'antd';
import { PlusOutlined, InboxOutlined, CompassOutlined } from '@ant-design/icons';

export default function ActionableEmptyState({
  title = 'Nenhum registro encontrado',
  description = 'Você ainda não possui dados cadastrados nesta seção.',
  actionLabel = 'Cadastrar Novo',
  onAction,
  icon = <InboxOutlined style={{ fontSize: 44, color: '#FF6600' }} />,
  secondaryActionLabel,
  onSecondaryAction,
}) {
  return (
    <div
      style={{
        padding: '48px 24px',
        textAlign: 'center',
        background: '#FFFFFF',
        borderRadius: 16,
        border: '1px dashed #E2E8F0',
        margin: '16px 0',
      }}
      className="animate-fade-in"
    >
      <div
        style={{
          width: 80,
          height: 80,
          borderRadius: '50%',
          background: 'rgba(255, 102, 0, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          margin: '0 auto 16px',
        }}
      >
        {icon}
      </div>

      <h3 style={{ fontSize: 18, color: '#0F172A', margin: '0 0 6px', fontWeight: 700 }}>
        {title}
      </h3>
      <p style={{ color: '#64748B', fontSize: 14, maxWidth: 440, margin: '0 auto 24px', lineHeight: 1.5 }}>
        {description}
      </p>

      <div style={{ display: 'flex', justifyContent: 'center', gap: 12, flexWrap: 'wrap' }}>
        {onAction && (
          <Button
            type="primary"
            size="large"
            icon={<PlusOutlined />}
            onClick={onAction}
            style={{ borderRadius: 10, height: 42, padding: '0 24px', fontWeight: 600 }}
          >
            {actionLabel}
          </Button>
        )}

        {onSecondaryAction && secondaryActionLabel && (
          <Button
            size="large"
            icon={<CompassOutlined />}
            onClick={onSecondaryAction}
            style={{ borderRadius: 10, height: 42, padding: '0 20px' }}
          >
            {secondaryActionLabel}
          </Button>
        )}
      </div>
    </div>
  );
}
