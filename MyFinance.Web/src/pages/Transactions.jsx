import React, { useEffect, useState } from 'react';
import { Table, Tag, Button, Modal, message, Card, Tooltip, Grid, Collapse, List, Segmented } from 'antd';
import { DeleteOutlined, EditOutlined, CloudUploadOutlined, PlusOutlined } from '@ant-design/icons';
import AddTransactionModal from '../components/AddTransactionModal';
import ImportModal from '../components/ImportModal';
import ActionableEmptyState from '../components/ActionableEmptyState';
import api from '../services/api';

const { useBreakpoint } = Grid;
const formatMoney = (value) => (value || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const installmentPattern = /\((\d+)\/(\d+)\)\s*$/;

const extractInstallmentInfo = (description) => {
  const match = description?.match(installmentPattern);
  if (!match) {
    return { installmentNumber: 1, totalInstallments: 1, baseDescription: description || '' };
  }

  return {
    installmentNumber: Number(match[1]),
    totalInstallments: Number(match[2]),
    baseDescription: description.replace(installmentPattern, '').trim(),
  };
};

const transactionAmount = (record) => {
  const isExpense = record.type === 'Expense';
  return {
    isExpense,
    label: `${isExpense ? '- ' : '+ '}${formatMoney(record.amount)}`,
  };
};

export default function Transactions({ month, year }) {
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [importBatches, setImportBatches] = useState([]);
  const [view, setView] = useState('consumption');

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    const controller = new AbortController();
    loadTransactions(controller.signal);
    loadImportBatches(controller.signal);

    return () => controller.abort();
  }, [month, year, view]);

  const loadTransactions = async (signal) => {
    setLoading(true);
    setTransactions([]);
    try {
      const params = new URLSearchParams({ view });
      if (month && year) { params.set('month', month); params.set('year', year); }
      const response = await api.get(`/transactions?${params.toString()}`, { signal });
      setTransactions(response.data || []);
    } catch (error) {
      if (signal?.aborted || error?.code === 'ERR_CANCELED') {
        return;
      }
      message.error('Erro ao carregar transações.');
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  };

  const loadImportBatches = async (signal) => {
    try {
      const response = await api.get('/import/batches', { signal });
      setImportBatches((response.data || []).filter((batch) => batch.reviewCount > 0 && batch.status !== 'confirmed'));
    } catch (error) {
      if (!signal?.aborted && error?.code !== 'ERR_CANCELED') console.error(error);
    }
  };

  const ignoreImportItem = async (itemId, batchId) => {
    try {
      await api.post(`/import/items/${itemId}/ignore`);
      message.success('Item ignorado.');
      const detail = await api.get(`/import/batches/${batchId}`);
      setImportBatches((current) => current.map((batch) => batch.id === batchId ? detail.data.batch : batch).filter((batch) => batch.reviewCount > 0));
    } catch { message.error('Não foi possível ignorar o item.'); }
  };

  const executeDelete = async (id, deleteAll) => {
    try {
      await api.delete(`/transactions/${id}?deleteAll=${deleteAll}`);
      message.success('Excluído com sucesso!');
      loadTransactions();
    } catch {
      message.error('Erro ao excluir');
    }
  };

  const handleDelete = (record) => {
    if (record.installmentId) {
      Modal.confirm({
        title: 'Excluir parcelamento',
        content: 'Esta transação faz parte de uma série. O que deseja fazer?',
        okText: 'Apagar TODAS',
        cancelText: 'Apenas ESTA',
        okButtonProps: { danger: true },
        onOk: () => executeDelete(record.id, true),
        onCancel: () => executeDelete(record.id, false),
      });
      return;
    }

    executeDelete(record.id, false);
  };

  const handleEdit = (record) => {
    if (record.isTransfer) {
      message.info('Transações de transferência devem ser ajustadas pelo fluxo de transferência.');
      return;
    }

    setEditingItem({
      ...record,
      ...extractInstallmentInfo(record.description),
    });
    setIsModalOpen(true);
  };

  const totalIncomes = transactions
    .filter((t) => t.type === 'Income')
    .reduce((acc, t) => acc + (t.amount || 0), 0);
  const totalExpenses = transactions
    .filter((t) => t.type === 'Expense')
    .reduce((acc, t) => acc + (t.amount || 0), 0);
  const netBalance = totalIncomes - totalExpenses;

  const columns = [
    {
      title: 'Descrição',
      dataIndex: 'description',
      key: 'desc',
      render: (t, record) => (
        <div>
          <span style={{ fontWeight: 600, color: '#0F172A', fontSize: 14 }}>{t}</span>
          {record.installmentId && (
            <Tag style={{ marginLeft: 8, fontSize: 11, borderRadius: 10, background: '#F1F5F9', border: '1px solid #E2E8F0', color: '#64748B' }}>
              Parcelado
            </Tag>
          )}
        </div>
      ),
    },
    {
      title: 'Categoria',
      dataIndex: ['category', 'name'],
      key: 'cat',
      render: (t) => (
        <span
          style={{
            display: 'inline-block',
            padding: '2px 10px',
            borderRadius: 12,
            background: '#F1F5F9',
            border: '1px solid #E2E8F0',
            fontSize: 12,
            fontWeight: 500,
            color: '#475569',
          }}
        >
          {t || 'Geral'}
        </span>
      ),
    },
    {
      title: 'Valor',
      dataIndex: 'amount',
      key: 'amt',
      render: (value, record) => {
        const isExpense = record.type === 'Expense';
        return (
          <span
            style={{
              color: isExpense ? '#F43F5E' : '#10B981',
              fontWeight: 700,
              fontSize: 14,
              fontFeatureSettings: '"tnum" 1',
            }}
          >
            {isExpense ? '- ' : '+ '}
            {formatMoney(value)}
          </span>
        );
      },
    },
    {
      title: 'Data',
      dataIndex: 'date',
      key: 'date',
      render: (d) => (
        <span style={{ color: '#64748B', fontSize: 13, fontFeatureSettings: '"tnum" 1' }}>
          {new Date(d).toLocaleDateString('pt-BR')}
        </span>
      ),
    },
    {
      title: 'Conta',
      dataIndex: ['account', 'name'],
      key: 'acc',
      render: (t) => <span style={{ color: '#475569', fontSize: 13 }}>{t || '-'}</span>,
    },
    {
      title: 'Ações',
      key: 'actions',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 4 }}>
          <Tooltip title="Editar">
            <Button
              type="text"
              size="small"
              icon={<EditOutlined style={{ color: '#64748B' }} />}
              onClick={() => handleEdit(record)}
            />
          </Tooltip>

          <Tooltip title="Excluir">
            <Button
              type="text"
              size="small"
              danger
              icon={<DeleteOutlined />}
              onClick={() => handleDelete(record)}
            />
          </Tooltip>
        </div>
      ),
    },
  ];

  const renderMobileTransaction = (record) => {
    const amount = transactionAmount(record);
    return (
      <article className="mobile-transaction-card" key={record.id}>
        <div className="mobile-transaction-card__top">
          <div className="mobile-transaction-card__description" title={record.description}>
            {record.description || 'Sem descrição'}
          </div>
          <span className="mobile-transaction-card__amount" style={{ color: amount.isExpense ? '#F43F5E' : '#10B981' }}>
            {amount.label}
          </span>
        </div>
        <div className="mobile-transaction-card__meta">
          <span>{new Date(record.date).toLocaleDateString('pt-BR')}</span>
          <span>{record.category?.name || 'Geral'}</span>
          <span>{record.account?.name || 'Sem conta'}</span>
          {record.installmentId && <Tag>Parcelado</Tag>}
          {record.status && <Tag>{record.status}</Tag>}
        </div>
        <div className="mobile-transaction-card__actions">
          <Tooltip title="Editar">
            <Button aria-label={`Editar ${record.description || 'transação'}`} type="text" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>
          <Tooltip title="Excluir">
            <Button aria-label={`Excluir ${record.description || 'transação'}`} type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(record)} />
          </Tooltip>
        </div>
      </article>
    );
  };

  return (
    <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 16,
          background: '#FFFFFF',
          padding: isCompact ? '16px' : '20px 24px',
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#0F172A', letterSpacing: '-0.02em' }}>
            Transações
          </h2>
          <span style={{ color: '#64748B', fontSize: 13 }}>
            Acompanhe e categorize suas entradas e saídas no período
          </span>
        </div>
        <div style={{ display: 'flex', gap: 10, width: isCompact ? '100%' : 'auto', flexWrap: 'wrap' }}>
          <Segmented
            value={view}
            onChange={setView}
            options={[
              { label: 'Consumo', value: 'consumption' },
              { label: 'Liquidações', value: 'settlements' },
              { label: 'Todas', value: 'all' },
            ]}
          />
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditingItem(null);
              setIsModalOpen(true);
            }}
            block={isCompact}
          >
            Nova Transação
          </Button>
          <Button
            icon={<CloudUploadOutlined />}
            onClick={() => setIsImportOpen(true)}
            block={isCompact}
          >
            Importar extrato
          </Button>
        </div>
      </div>

      {/* Mini-Cards de Resumo do Período */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: isCompact ? '1fr' : 'repeat(3, 1fr)',
          gap: 16,
        }}
      >
        <div
          style={{
            background: '#FFFFFF',
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            Entradas do Período
          </div>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#10B981', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            +{formatMoney(totalIncomes)}
          </div>
        </div>

        <div
          style={{
            background: '#FFFFFF',
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            Saídas do Período
          </div>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#F43F5E', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            -{formatMoney(totalExpenses)}
          </div>
        </div>

        <div
          style={{
            background: '#FFFFFF',
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            Resultado Líquido
          </div>
          <div
            style={{
              fontSize: 22,
              fontWeight: 700,
              color: netBalance >= 0 ? '#0F172A' : '#F43F5E',
              marginTop: 4,
              fontFeatureSettings: '"tnum" 1',
            }}
          >
            {formatMoney(netBalance)}
          </div>
        </div>
      </div>

      <Card
        variant="borderless"
        style={{
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
        bodyStyle={{ padding: isCompact ? 12 : 20 }}
      >
        {!loading && transactions.length === 0 ? (
          <ActionableEmptyState
            title="Nenhuma transação encontrada no mês"
            description="Você pode adicionar transações manualmente ou importar extratos via CSV para automatizar seu controle."
            actionLabel="Criar Nova Transação"
            onAction={() => {
              setEditingItem(null);
              setIsModalOpen(true);
            }}
            secondaryActionLabel="Importar Extrato CSV"
            onSecondaryAction={() => setIsImportOpen(true)}
          />
        ) : (
          isCompact ? (
            loading ? <List loading dataSource={[]} /> : <div className="mobile-transaction-list">{transactions.map(renderMobileTransaction)}</div>
          ) : (
            <div className="responsive-table-wrap">
              <Table
                dataSource={transactions}
                columns={columns}
                rowKey="id"
                loading={loading}
                pagination={{ pageSize: 10 }}
                size="middle"
              />
            </div>
          )
        )}
      </Card>

      {importBatches.length > 0 && (
        <Card
          title="Revisão de importação"
          variant="borderless"
          style={{
            marginTop: 8,
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <Collapse
            items={importBatches.map((batch) => ({
              key: batch.id,
              label: `${batch.fileName} · ${batch.reviewCount} item(ns) pendente(s)`,
              children: <ImportReviewItems batchId={batch.id} onIgnore={(id) => ignoreImportItem(id, batch.id)} />,
            }))}
          />
        </Card>
      )}

      <AddTransactionModal
        visible={isModalOpen}
        transactionToEdit={editingItem}
        onClose={() => {
          setIsModalOpen(false);
          setEditingItem(null);
        }}
        onSuccess={() => loadTransactions()}
      />

      <ImportModal
        visible={isImportOpen}
        onClose={() => setIsImportOpen(false)}
        onSuccess={() => {
          loadTransactions();
          loadImportBatches();
        }}
      />
    </div>
  );
}

function ImportReviewItems({ batchId, onIgnore }) {
  const [items, setItems] = useState([]);
  useEffect(() => { api.get(`/import/batches/${batchId}`).then((response) => setItems((response.data.items || []).filter((item) => item.status === 'needs_review'))).catch(() => {}); }, [batchId]);
  return <List size="small" dataSource={items} locale={{ emptyText: 'Nenhum item pendente.' }} renderItem={(item) => <List.Item actions={[<Button key="ignore" size="small" onClick={() => onIgnore(item.id)}>Ignorar</Button>]}><List.Item.Meta title={item.memo} description={`${new Date(item.postedAt).toLocaleDateString('pt-BR')} · ${item.reason || 'Revisão necessária'}`} /></List.Item>} />;
}
