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

  const columns = [
    { title: 'Descrição', dataIndex: 'description', key: 'desc', render: (t) => <strong>{t}</strong> },
    {
      title: 'Categoria',
      dataIndex: ['category', 'name'],
      key: 'cat',
      render: (t) => <Tag color="orange">{t || 'Geral'}</Tag>,
    },
    {
      title: 'Valor',
      dataIndex: 'amount',
      key: 'amt',
      render: (value, record) => (
        <span style={{ color: record.type === 'Expense' ? '#EF4444' : '#10B981', fontWeight: 'bold' }}>
          {record.type === 'Expense' ? '- ' : '+ '}
          {formatMoney(value)}
        </span>
      ),
    },
    {
      title: 'Data',
      dataIndex: 'date',
      key: 'date',
      render: (d) => new Date(d).toLocaleDateString('pt-BR'),
    },
    {
      title: 'Conta',
      dataIndex: ['account', 'name'],
      key: 'acc',
      render: (t) => t || '-',
    },
    {
      title: 'Ações',
      key: 'actions',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8 }}>
          <Tooltip title="Editar">
            <Button type="text" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>

          <Tooltip title="Excluir">
            <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(record)} />
          </Tooltip>
        </div>
      ),
    },
  ];

  return (
    <div className="animate-fade-in">
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          marginBottom: 16,
        }}
      >
        <h2 style={{ margin: 0 }}>Extrato de Transações</h2>
        <div style={{ display: 'flex', gap: 10, width: isCompact ? '100%' : 'auto', flexWrap: 'wrap' }}>
          <Segmented value={view} onChange={setView} options={[{ label: 'Consumo', value: 'consumption' }, { label: 'Liquidações', value: 'settlements' }, { label: 'Todas', value: 'all' }]} />
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

      <Card variant="borderless" style={{ borderRadius: 16 }} bodyStyle={{ padding: isCompact ? 12 : 24 }}>
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
          <Table
            dataSource={transactions}
            columns={columns}
            rowKey="id"
            loading={loading}
            pagination={{ pageSize: isCompact ? 8 : 10 }}
            size={isCompact ? 'small' : 'middle'}
            scroll={{ x: 860 }}
          />
        )}
      </Card>

      {importBatches.length > 0 && <Card title="Revisão de importação" variant="borderless" style={{ marginTop: 16, borderRadius: 16 }}>
        <Collapse items={importBatches.map((batch) => ({
          key: batch.id,
          label: `${batch.fileName} · ${batch.reviewCount} item(ns) pendente(s)`,
          children: <ImportReviewItems batchId={batch.id} onIgnore={(id) => ignoreImportItem(id, batch.id)} />,
        }))} />
      </Card>}

      <AddTransactionModal
        visible={isModalOpen}
        transactionToEdit={editingItem}
        onClose={() => {
          setIsModalOpen(false);
          setEditingItem(null);
        }}
        onSuccess={() => loadTransactions()}
      />

      <ImportModal visible={isImportOpen} onClose={() => setIsImportOpen(false)} onSuccess={() => { loadTransactions(); loadImportBatches(); }} />
    </div>
  );
}

function ImportReviewItems({ batchId, onIgnore }) {
  const [items, setItems] = useState([]);
  useEffect(() => { api.get(`/import/batches/${batchId}`).then((response) => setItems((response.data.items || []).filter((item) => item.status === 'needs_review'))).catch(() => {}); }, [batchId]);
  return <List size="small" dataSource={items} locale={{ emptyText: 'Nenhum item pendente.' }} renderItem={(item) => <List.Item actions={[<Button key="ignore" size="small" onClick={() => onIgnore(item.id)}>Ignorar</Button>]}><List.Item.Meta title={item.memo} description={`${new Date(item.postedAt).toLocaleDateString('pt-BR')} · ${item.reason || 'Revisão necessária'}`} /></List.Item>} />;
}
