import React, { useEffect, useState } from 'react';
import { Card, Select, Tabs, List, Tag, Statistic, Grid, message, Skeleton } from 'antd';
import api from '../services/api';
import dayjs from 'dayjs';

const { useBreakpoint } = Grid;
const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Invoices() {
  const [cards, setCards] = useState([]);
  const [selectedCard, setSelectedCard] = useState(null);
  const [invoiceData, setInvoiceData] = useState(null);
  const [currentMonth, setCurrentMonth] = useState(dayjs());
  const [cardsLoading, setCardsLoading] = useState(true);
  const [invoiceLoading, setInvoiceLoading] = useState(false);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    let cancelled = false;

    const loadCards = async () => {
      try {
        setCardsLoading(true);
        const res = await api.get('/accounts');
        if (cancelled) return;

        const c = res.data.filter((a) => a.isCreditCard);
        setCards(c);
        if (c.length > 0) {
          setSelectedCard(c[0].id);
        }
      } catch (error) {
        if (!cancelled) {
          console.error('Erro ao carregar cartoes para faturas:', error);
          message.error('Nao foi possivel carregar os cartoes.');
          setCards([]);
        }
      } finally {
        if (!cancelled) {
          setCardsLoading(false);
        }
      }
    };

    loadCards();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!selectedCard) return undefined;

    let cancelled = false;

    const fetchInvoice = async () => {
      try {
        setInvoiceLoading(true);
        setInvoiceData(null);
        const response = await api.get(
          `/transactions/invoice?accountId=${selectedCard}&month=${currentMonth.month() + 1}&year=${currentMonth.year()}`,
        );
        if (!cancelled) {
          setInvoiceData(response.data);
        }
      } catch (error) {
        if (!cancelled) {
          console.error('Erro ao carregar dados da fatura:', error);
          message.error('Nao foi possivel carregar a fatura selecionada.');
          setInvoiceData({ total: 0, dueDate: dayjs().toISOString(), transactions: [] });
        }
      } finally {
        if (!cancelled) {
          setInvoiceLoading(false);
        }
      }
    };

    fetchInvoice();

    return () => {
      cancelled = true;
    };
  }, [selectedCard, currentMonth]);

  const items = Array.from({ length: 6 }).map((_, i) => {
    const date = dayjs().add(i - 2, 'month');
    return {
      key: date.format('YYYY-MM'),
      label: date.format('MMM/YY'),
    };
  });

  return (
    <div>
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
          marginBottom: 16,
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#0F172A', letterSpacing: '-0.02em' }}>
            Faturas de Cartão
          </h2>
          <span style={{ color: '#64748B', fontSize: 13 }}>
            Acompanhe o fechamento, vencimento e gastos detalhados de cada cartão
          </span>
        </div>
        <Select
          style={{ width: isCompact ? '100%' : 260 }}
          value={selectedCard}
          onChange={setSelectedCard}
          placeholder="Selecione um cartão"
          loading={cardsLoading}
          options={cards.map((c) => ({ label: c.name, value: c.id }))}
        />
      </div>

      <Card
        variant="borderless"
        style={{
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
        bodyStyle={{ padding: isCompact ? 12 : 24 }}
      >
        <Tabs
          activeKey={currentMonth.format('YYYY-MM')}
          onChange={(key) => setCurrentMonth(dayjs(key))}
          items={items.map((item) => ({
            label: item.label,
            key: item.key,
            children: invoiceLoading ? (
              <div style={{ padding: isCompact ? 8 : 12 }}>
                <Skeleton active paragraph={{ rows: 5 }} title={{ width: '35%' }} />
              </div>
            ) : invoiceData ? (
              <div>
                <div
                  style={{
                    display: 'flex',
                    flexDirection: isCompact ? 'column' : 'row',
                    justifyContent: 'space-between',
                    alignItems: isCompact ? 'stretch' : 'center',
                    gap: 16,
                    marginBottom: 20,
                    padding: isCompact ? 16 : 20,
                    background: '#F8FAFC',
                    borderRadius: 12,
                    border: '1px solid #E2E8F0',
                  }}
                >
                  <div>
                    <div style={{ color: '#64748B', fontSize: 13, fontWeight: 500 }}>
                      Vencimento: <strong style={{ color: '#0F172A' }}>{dayjs(invoiceData.dueDate).format('DD/MM/YYYY')}</strong>
                    </div>
                    <div style={{ marginTop: 6, display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 13, color: '#64748B' }}>Status:</span>
                      <span
                        style={{
                          padding: '2px 10px',
                          borderRadius: 12,
                          fontSize: 12,
                          fontWeight: 600,
                          background: invoiceData.total > 0 ? '#FFFBEB' : '#ECFDF5',
                          border: `1px solid ${invoiceData.total > 0 ? '#FDE68A' : '#A7F3D0'}`,
                          color: invoiceData.total > 0 ? '#B45309' : '#047857',
                        }}
                      >
                        {invoiceData.total > 0 ? 'Fatura Aberta' : 'Fatura Liquidada'}
                      </span>
                    </div>
                  </div>
                  <div>
                    <span style={{ fontSize: 11, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 600 }}>
                      Valor Total da Fatura
                    </span>
                    <div
                      style={{
                        fontSize: isCompact ? 22 : 26,
                        fontWeight: 700,
                        color: invoiceData.total > 0 ? '#F43F5E' : '#10B981',
                        marginTop: 2,
                        fontFeatureSettings: '"tnum" 1',
                      }}
                    >
                      {formatMoney(invoiceData.total)}
                    </div>
                  </div>
                </div>

                <List
                  itemLayout="horizontal"
                  dataSource={invoiceData.transactions}
                  locale={{ emptyText: 'Nenhum lançamento nesta fatura.' }}
                  renderItem={(invoiceItem) => (
                    <List.Item style={{ padding: '12px 8px', borderBottom: '1px solid #F1F5F9' }}>
                      <List.Item.Meta
                        title={<span style={{ fontWeight: 600, color: '#0F172A', fontSize: 14 }}>{invoiceItem.description}</span>}
                        description={<span style={{ color: '#64748B', fontSize: 12 }}>{dayjs(invoiceItem.date).format('DD/MM/YYYY')}</span>}
                      />
                      <div
                        style={{
                          fontWeight: 700,
                          textAlign: 'right',
                          color: '#0F172A',
                          fontSize: 14,
                          fontFeatureSettings: '"tnum" 1',
                        }}
                      >
                        {formatMoney(invoiceItem.amount)}
                      </div>
                    </List.Item>
                  )}
                />

                {invoiceData.categorySummary?.length > 0 && (
                  <Card
                    size="small"
                    title={<span style={{ fontSize: 14, fontWeight: 600, color: '#0F172A' }}>Compras por categoria</span>}
                    variant="borderless"
                    style={{ marginTop: 20, borderRadius: 12, border: '1px solid #E2E8F0', background: '#F8FAFC' }}
                  >
                    {invoiceData.categorySummary.map((item) => (
                      <div
                        key={item.categoryId || item.name}
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          padding: '8px 0',
                          borderBottom: '1px solid #EDF2F7',
                          fontSize: 13,
                        }}
                      >
                        <span style={{ color: '#475569' }}>{item.name}</span>
                        <strong style={{ color: '#0F172A', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(item.total)}</strong>
                      </div>
                    ))}
                  </Card>
                )}

                {invoiceData.settlements?.length > 0 && (
                  <Card
                    size="small"
                    title={<span style={{ fontSize: 14, fontWeight: 600, color: '#0F172A' }}>Liquidações e Pagamentos</span>}
                    variant="borderless"
                    style={{ marginTop: 16, borderRadius: 12, border: '1px solid #E2E8F0', background: '#F8FAFC' }}
                  >
                    {invoiceData.settlements.map((item) => (
                      <div
                        key={item.id}
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          padding: '8px 0',
                          borderBottom: '1px solid #EDF2F7',
                          fontSize: 13,
                        }}
                      >
                        <span style={{ color: '#475569' }}>{dayjs(item.date).format('DD/MM/YYYY')} · Pagamento de fatura</span>
                        <strong style={{ color: '#10B981', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(item.amount)}</strong>
                      </div>
                    ))}
                  </Card>
                )}
              </div>
            ) : (
              <p style={{ color: '#64748B' }}>Carregando dados da fatura...</p>
            ),
          }))}
        />
      </Card>
    </div>
  );
}
