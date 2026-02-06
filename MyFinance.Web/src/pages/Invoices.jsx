import React, { useEffect, useState } from 'react';
import { Card, Select, Tabs, List, Tag, Button, Statistic, message } from 'antd';
import { CreditCardOutlined, CalendarOutlined } from '@ant-design/icons';
import api from '../services/api';
import dayjs from 'dayjs';

const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Invoices() {
  const [cards, setCards] = useState([]);
  const [selectedCard, setSelectedCard] = useState(null);
  const [invoiceData, setInvoiceData] = useState(null);
  const [currentMonth, setCurrentMonth] = useState(dayjs());

  useEffect(() => {
    api.get('/accounts').then(res => {
        const c = res.data.filter(a => a.isCreditCard);
        setCards(c);
        if (c.length > 0) setSelectedCard(c[0].id);
    });
  }, []);

  useEffect(() => {
    if (selectedCard) {
        setInvoiceData(null); // <--- LIMPA A TELA ANTES DE CARREGAR O NOVO
        loadInvoice();
    }
  }, [selectedCard, currentMonth]);

  const loadInvoice = async () => {
    try {
        const response = await api.get(`/transactions/invoice?accountId=${selectedCard}&month=${currentMonth.month() + 1}&year=${currentMonth.year()}`);
        setInvoiceData(response.data);
    } catch (error) {
        // message.error('Erro ao carregar fatura');
    }
  };

  const items = Array.from({ length: 6 }).map((_, i) => {
      const date = dayjs().add(i - 2, 'month'); // 2 meses atrs at 3 pra frente
      return {
        key: date.format('YYYY-MM'),
        label: date.format('MMM/YY'),
      };
  });

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 24 }}>
        <h2 style={{ margin: 0 }}>Faturas do Cartão</h2>
        <Select 
            style={{ width: 200 }} 
            value={selectedCard} 
            onChange={setSelectedCard}
            options={cards.map(c => ({ label: c.name, value: c.id }))}
        />
      </div>

      <Card>
        <Tabs 
            activeKey={currentMonth.format('YYYY-MM')}
            onChange={(key) => setCurrentMonth(dayjs(key))}
            items={items.map(item => ({
                label: item.label,
                key: item.key,
                children: (
                    invoiceData ? (
                        <div>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, padding: 20, background: '#f9f9f9', borderRadius: 8 }}>
                                <div>
                                    <div style={{ color: '#888' }}>Vencimento {dayjs(invoiceData.dueDate).format('DD/MM/YYYY')}</div>
                                    <div style={{ fontWeight: 'bold' }}>Status: <Tag color={invoiceData.total > 0 ? 'orange' : 'green'}>{invoiceData.total > 0 ? 'Aberta' : 'Paga'}</Tag></div>
                                </div>
                                <Statistic title="Valor da Fatura" value={invoiceData.total} formatter={formatMoney} valueStyle={{ color: '#cf1322' }} />
                            </div>

                            <List
                                itemLayout="horizontal"
                                dataSource={invoiceData.transactions}
                                renderItem={item => (
                                    <List.Item>
                                        <List.Item.Meta
                                            avatar={<div style={{ width: 40, height: 40, background: '#eee', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}> </div>}
                                            title={item.description}
                                            description={dayjs(item.date).format('DD/MM/YYYY')}
                                        />
                                        <div style={{ fontWeight: 'bold' }}>{formatMoney(item.amount)}</div>
                                    </List.Item>
                                )}
                            />
                        </div>
                    ) : <p>Carregando...</p>
                )
            }))}
        />
      </Card>
    </div>
  );
}
