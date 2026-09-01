import React, { useEffect, useState } from 'react';
import { Modal, Select, Upload, Button, message, Grid, Steps, Table, Tag, Statistic, Row, Col } from 'antd';
import { InboxOutlined, BankOutlined, CreditCardOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Dragger } = Upload;
const { Option } = Select;
const { useBreakpoint } = Grid;

export default function ImportModal({ visible, onClose, onSuccess }) {
  const [accounts, setAccounts] = useState([]);
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [fileList, setFileList] = useState([]);
  const [batch, setBatch] = useState(null);
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const screens = useBreakpoint();

  useEffect(() => {
    if (visible) { api.get('/accounts').then((r) => setAccounts(r.data || [])).catch(() => message.error('Erro ao carregar contas')); setFileList([]); setSelectedAccount(null); setBatch(null); setStep(0); }
  }, [visible]);

  const upload = async () => {
    if (!selectedAccount || !fileList.length) return message.error('Selecione a conta e o arquivo.');
    const form = new FormData(); form.append('file', fileList[0]); setLoading(true);
    try { const response = await api.post(`/import/upload?accountId=${selectedAccount}`, form, { headers: { 'Content-Type': 'multipart/form-data' } }); setBatch(response.data); setStep(1); }
    catch (error) { message.error(error.response?.data || 'Erro na importacao.'); }
    finally { setLoading(false); }
  };

  const confirm = async () => {
    setLoading(true);
    try { await api.post(`/import/batches/${batch.batch.id}/confirm`); message.success('Importacao confirmada.'); onSuccess(); onClose(); }
    catch (error) { message.error(error.response?.data || 'Erro ao confirmar importacao.'); }
    finally { setLoading(false); }
  };

  const columns = [
    { title: 'Data', dataIndex: 'postedAt', render: (v) => new Date(v).toLocaleDateString('pt-BR') },
    { title: 'Descricao', dataIndex: 'memo' },
    { title: 'Valor', dataIndex: 'signedAmount', render: (v) => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) },
    { title: 'Status', dataIndex: 'status', render: (v) => <Tag color={v === 'needs_review' ? 'orange' : v === 'probable_duplicate' ? 'red' : 'green'}>{v}</Tag> },
  ];

  return <Modal title="Importar extrato" open={visible} onCancel={onClose} width={screens.md ? 760 : 'calc(100vw - 20px)'} destroyOnClose footer={step === 0 ? [<Button key="cancel" onClick={onClose}>Cancelar</Button>, <Button key="upload" type="primary" loading={loading} onClick={upload}>Gerar prévia</Button>] : [<Button key="back" onClick={() => setStep(0)}>Voltar</Button>, <Button key="confirm" type="primary" loading={loading} onClick={confirm}>Confirmar itens seguros</Button>]}>
    <Steps current={step} items={[{ title: 'Upload' }, { title: 'Prévia e revisão' }]} style={{ marginBottom: 24 }} />
    {step === 0 ? <>
      <Select style={{ width: '100%', marginBottom: 16 }} placeholder="Conta ou cartão" value={selectedAccount} onChange={setSelectedAccount} showSearch optionFilterProp="children">{accounts.map((a) => <Option key={a.id} value={a.id}>{a.isCreditCard ? <CreditCardOutlined /> : <BankOutlined />} {a.name}</Option>)}</Select>
      <Dragger beforeUpload={(file) => { const ok = /\.(csv|xlsx|ofx)$/i.test(file.name); if (!ok) message.error('Apenas OFX, CSV ou XLSX.'); else setFileList([file]); return false; }} onRemove={() => setFileList([])} fileList={fileList}><p className="ant-upload-drag-icon"><InboxOutlined /></p><p className="ant-upload-text">Arraste OFX, CSV ou XLSX</p><p className="ant-upload-hint">A prévia não altera seus lançamentos.</p></Dragger>
    </> : <>
      <Row gutter={12} style={{ marginBottom: 16 }}>{[['Importados', batch?.batch.importedCount], ['Revisao', batch?.batch.reviewCount], ['Duplicados', batch?.batch.duplicateCount], ['Ignorados', batch?.batch.ignoredCount]].map(([title, value]) => <Col span={6} key={title}><Statistic title={title} value={value || 0} /></Col>)}</Row>
      {batch?.reconciliation?.ledgerBalance != null && <p>Saldo oficial: <strong>{Number(batch.reconciliation.ledgerBalance).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</strong> · Diferença: {Number(batch.reconciliation.difference || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</p>}
      <Table size="small" rowKey="id" columns={columns} dataSource={batch?.items || []} pagination={{ pageSize: 6 }} />
      <p style={{ color: '#666' }}>Itens em revisão não serão lançados. Resolva-os na seção de revisão de importação antes de confirmar.</p>
    </>}
  </Modal>;
}
