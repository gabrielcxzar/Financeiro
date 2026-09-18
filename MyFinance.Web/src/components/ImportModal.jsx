import React, { useEffect, useState } from 'react';
import { Alert, Button, List, Modal, Select, Upload, message, Grid, Steps, Table, Tag, Statistic, Row, Col, Tabs, Space } from 'antd';
import { InboxOutlined, GoogleOutlined, SyncOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Dragger } = Upload;
const { useBreakpoint } = Grid;

export default function ImportModal({ visible, onClose, onSuccess }) {
  const [accounts, setAccounts] = useState([]);
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [fileList, setFileList] = useState([]);
  const [batch, setBatch] = useState(null);
  const [gmailBatches, setGmailBatches] = useState([]);
  const [gmailStatus, setGmailStatus] = useState(null);
  const [mode, setMode] = useState('file');
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [syncResult, setSyncResult] = useState(null);
  const screens = useBreakpoint();

  const loadGmail = async () => {
    try {
      const [status, batches] = await Promise.all([api.get('/integrations/gmail/status'), api.get('/integrations/gmail/batches')]);
      setGmailStatus(status.data);
      setGmailBatches(batches.data || []);
    } catch {
      setGmailStatus(null);
    }
  };

  useEffect(() => {
    if (!visible) return;
    api.get('/accounts').then((r) => setAccounts(r.data || [])).catch(() => message.error('Erro ao carregar contas'));
    setFileList([]); setSelectedAccount(null); setBatch(null); setStep(0); setMode('file'); setSyncResult(null);
    loadGmail();
  }, [visible]);

  const upload = async () => {
    if (!selectedAccount || !fileList.length) return message.error('Selecione a conta e o arquivo.');
    const form = new FormData(); form.append('file', fileList[0]); setLoading(true);
    try { const response = await api.post(`/import/upload?accountId=${selectedAccount}`, form, { headers: { 'Content-Type': 'multipart/form-data' } }); setBatch(response.data); setStep(1); }
    catch (error) { message.error(error.response?.data || 'Erro na importação.'); }
    finally { setLoading(false); }
  };

  const connectGmail = async () => {
    try { const response = await api.get('/integrations/gmail/connect'); window.location.assign(response.data.authorizationUrl); }
    catch (error) { message.error(error.response?.data || 'Gmail não configurado neste ambiente.'); }
  };

  const syncGmail = async () => {
    setLoading(true);
    try { const response = await api.post('/integrations/gmail/sync'); setSyncResult(response.data); await loadGmail(); }
    catch (error) { message.error(error.response?.data || 'Erro ao sincronizar o Gmail.'); }
    finally { setLoading(false); }
  };

  const openGmailBatch = async (id) => {
    setLoading(true);
    try { const response = await api.get(`/import/batches/${id}`); setBatch(response.data); setStep(1); }
    catch { message.error('Não foi possível abrir a prévia do lote.'); }
    finally { setLoading(false); }
  };

  const confirm = async () => {
    setLoading(true);
    try { await api.post(`/import/batches/${batch.batch.id}/confirm`); message.success('Importação confirmada.'); onSuccess(); onClose(); }
    catch (error) { message.error(error.response?.data || 'Erro ao confirmar importação.'); }
    finally { setLoading(false); }
  };

  const columns = [
    { title: 'Data', dataIndex: 'postedAt', render: (v) => new Date(v).toLocaleDateString('pt-BR') },
    { title: 'Descrição', dataIndex: 'memo' },
    { title: 'Valor', dataIndex: 'signedAmount', render: (v) => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) },
    { title: 'Status', dataIndex: 'status', render: (v) => <Tag color={v === 'needs_review' ? 'orange' : v === 'probable_duplicate' ? 'red' : 'green'}>{v}</Tag> },
  ];

  const preview = step === 1 && batch ? <>
    <Row gutter={12} style={{ marginBottom: 16 }}>{[['Importados', batch.batch.importedCount], ['Revisão', batch.batch.reviewCount], ['Duplicados', batch.batch.duplicateCount], ['Ignorados', batch.batch.ignoredCount]].map(([title, value]) => <Col span={6} key={title}><Statistic title={title} value={value || 0} /></Col>)}</Row>
    {batch.reconciliation?.ledgerBalance != null && <p>Saldo oficial: <strong>{Number(batch.reconciliation.ledgerBalance).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</strong> · Diferença: {Number(batch.reconciliation.difference || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</p>}
    <Table size="small" rowKey="id" columns={columns} dataSource={batch.items || []} pagination={{ pageSize: 6 }} />
    <p style={{ color: '#666' }}>Itens em revisão não serão lançados. Resolva-os antes de confirmar.</p>
  </> : null;

  return <Modal title="Importar extrato" open={visible} onCancel={onClose} width={screens.md ? 760 : 'calc(100vw - 20px)'} destroyOnClose footer={step === 0 ? [<Button key="cancel" onClick={onClose}>Cancelar</Button>, mode === 'file' ? <Button key="upload" type="primary" loading={loading} onClick={upload}>Gerar prévia</Button> : null, mode === 'gmail' && gmailStatus?.connected ? <Button key="sync" type="primary" icon={<SyncOutlined />} loading={loading} onClick={syncGmail}>Sincronizar agora</Button> : null] : [<Button key="back" onClick={() => setStep(0)}>Voltar</Button>, <Button key="confirm" type="primary" loading={loading} onClick={confirm}>Confirmar itens seguros</Button>]}>
    <Tabs activeKey={mode} onChange={(key) => { setMode(key); setStep(0); }} items={[{ key: 'file', label: 'Arquivo' }, { key: 'gmail', label: 'Gmail', icon: <GoogleOutlined /> }]} />
    <Steps current={step} items={mode === 'gmail' ? [{ title: 'Gmail' }, { title: 'Prévia e revisão' }] : [{ title: 'Upload' }, { title: 'Prévia e revisão' }]} style={{ marginBottom: 24 }} />
    {step === 1 ? preview : mode === 'file' ? <>
      <Select style={{ width: '100%', marginBottom: 16 }} placeholder="Conta ou cartão" value={selectedAccount} onChange={setSelectedAccount} showSearch optionFilterProp="label" options={accounts.map((a) => ({ value: a.id, label: `${a.isCreditCard ? 'Cartão' : 'Conta'} · ${a.name}` }))} />
      <Dragger beforeUpload={(file) => { const ok = /\.(csv|xlsx|ofx)$/i.test(file.name); if (!ok) message.error('Apenas OFX, CSV ou XLSX.'); else setFileList([file]); return false; }} onRemove={() => setFileList([])} fileList={fileList}><p className="ant-upload-drag-icon"><InboxOutlined /></p><p className="ant-upload-text">Arraste OFX, CSV ou XLSX</p><p className="ant-upload-hint">A prévia não altera seus lançamentos.</p></Dragger>
    </> : <>
      {!gmailStatus?.enabled && <Alert type="info" showIcon message="Gmail não configurado neste ambiente." description="Configure as variáveis Google no backend para habilitar a conexão." />}
      {gmailStatus?.enabled && !gmailStatus.connected && <Button type="primary" icon={<GoogleOutlined />} onClick={connectGmail}>Conectar Gmail</Button>}
      {gmailStatus?.connected && <>
        <Alert type="success" showIcon message={`Conectado: ${gmailStatus.googleEmail || 'conta Google'}`} description="O Gmail é usado apenas para localizar anexos OFX. Todo lote exige revisão e confirmação humana." style={{ marginBottom: 16 }} />
        {syncResult && <Alert type={syncResult.ruleErrors ? 'warning' : 'success'} showIcon message="Sincronização concluída" description={<Space direction="vertical"><span>{syncResult.found} anexo(s) OFX encontrado(s) · {syncResult.newBatches} novo(s) lote(s) · {syncResult.alreadyProcessed} já processado(s) · {syncResult.invalid} inválido(s) · {syncResult.reviewItems} item(ns) aguardando revisão.</span>{syncResult.found === 0 && <span>Nenhum anexo OFX foi encontrado com as regras atuais.</span>}{syncResult.found > 0 && syncResult.newBatches === 0 && syncResult.alreadyProcessed > 0 && <span>Todos os extratos encontrados já haviam sido processados.</span>}{syncResult.newBatches > 0 && gmailBatches[0] && <Button size="small" type="link" onClick={() => openGmailBatch(gmailBatches[0].batchId)}>Revisar agora</Button>}</Space>} style={{ marginBottom: 16 }} />}
        <List bordered dataSource={gmailBatches} locale={{ emptyText: 'Nenhum lote Gmail encontrado.' }} renderItem={(item) => <List.Item actions={[<Button key="review" onClick={() => openGmailBatch(item.batchId)}>Revisar agora</Button>]}><List.Item.Meta title={item.fileName} description={`${item.reviewCount || 0} item(ns) em revisão · ${new Date(item.createdAt).toLocaleString('pt-BR')}`} /></List.Item>} />
      </>}
    </>}
  </Modal>;
}
