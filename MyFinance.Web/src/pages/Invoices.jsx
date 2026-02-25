import React, { useEffect, useState } from 'react';
import { Card, Select, Tabs, List, Tag, Statistic, Grid } from 'antd';
import api from '../services/api';
import dayjs from 'dayjs';

const { useBreakpoint } = Grid;
const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Invoices() {
  const [cards, setCards] = useState([]);
  const [selectedCard, setSelectedCard] = useState(null);
  const [invoiceData, setInvoiceData] = useState(null);
  const [currentMonth, setCurrentMonth] = useState(dayjs());

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    api.get('/accounts').then((res) => {
      const c = res.data.filter((a) => a.isCreditCard);
      setCards(c);
      if (c.length > 0) setSelectedCard(c[0].id);
    });
  }, []);

  useEffect(() => {
    if (!selectedCard) return undefined;

    let cancelled = false;

    const fetchInvoice = async () => {
      try {
        const response = await api.get(
          `/transactions/invoice?accountId=${selectedCard}&month=${currentMonth.month() + 1}&year=${currentMonth.year()}`,
        );
        if (!cancelled) {
          setInvoiceData(response.data);
        }
      } catch {
        if (!cancelled) {
          setInvoiceData({ total: 0, dueDate: dayjs().toISOString(), transactions: [] });
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
          marginBottom: 16,
          gap: 12,
        }}
      >
        <h2 style={{ margin: 0 }}>Faturas do Cartao</h2>
        <Select
          style={{ width: isCompact ? '100%' : 240 }}
          value={selectedCard}
          onChange={setSelectedCard}
          placeholder="Selecione um cartao"
          options={cards.map((c) => ({ label: c.name, value: c.id }))}
        />
      </div>

      <Card bodyStyle={{ padding: isCompact ? 10 : 24 }}>
        <Tabs
          activeKey={currentMonth.format('YYYY-MM')}
          onChange={(key) => setCurrentMonth(dayjs(key))}
          items={items.map((item) => ({
            label: item.label,
            key: item.key,
            children: invoiceData ? (
              <div>
                <div
                  style={{
                    display: 'flex',
                    flexDirection: isCompact ? 'column' : 'row',
                    justifyContent: 'space-between',
                    alignItems: isCompact ? 'stretch' : 'center',
                    gap: 12,
                    marginBottom: 16,
                    padding: isCompact ? 12 : 20,
                    background: '#f9f9f9',
                    borderRadius: 8,
                  }}
                >
                  <div>
                    <div style={{ color: '#888' }}>Vencimento {dayjs(invoiceData.dueDate).format('DD/MM/YYYY')}</div>
                    <div style={{ fontWeight: 'bold' }}>
                      Status:{' '}
                      <Tag color={invoiceData.total > 0 ? 'orange' : 'green'}>
                        {invoiceData.total > 0 ? 'Aberta' : 'Paga'}
                      </Tag>
                    </div>
                  </div>
                  <Statistic title="Valor da Fatura" value={invoiceData.total} formatter={formatMoney} valueStyle={{ color: '#cf1322' }} />
                </div>

                <List
                  itemLayout="horizontal"
                  dataSource={invoiceData.transactions}
                  locale={{ emptyText: 'Nenhum lancamento nesta fatura.' }}
                  renderItem={(invoiceItem) => (
                    <List.Item>
                      <List.Item.Meta
                        avatar={
                          <div
                            style={{
                              width: 36,
                              height: 36,
                              background: '#eef3fb',
                              borderRadius: '50%',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                            }}
                          />
                        }
                        title={invoiceItem.description}
                        description={dayjs(invoiceItem.date).format('DD/MM/YYYY')}
                      />
                      <div style={{ fontWeight: 'bold', textAlign: 'right', marginLeft: 10 }}>
                        {formatMoney(invoiceItem.amount)}
                      </div>
                    </List.Item>
                  )}
                />
              </div>
            ) : (
              <p>Carregando...</p>
            ),
          }))}
        />
      </Card>
    </div>
  );
}
