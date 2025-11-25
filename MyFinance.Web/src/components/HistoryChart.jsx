import React, { useMemo } from 'react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  PointElement,
  LineElement
} from 'chart.js';
import { Chart } from 'react-chartjs-2';

// Registra todos os componentes visuais do gráfico
ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
);

export default function HistoryChart({ transactions }) {
  
  const chartData = useMemo(() => {
    // 1. Agrupar transações por Mês (Chave: "2025-11")
    const groups = {};

    transactions.forEach(t => {
      const date = new Date(t.date);
      // Cria uma chave ordenável: "2025-11"
      const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
      
      if (!groups[key]) {
        groups[key] = { income: 0, expense: 0, balance: 0, label: key };
      }

      if (t.type === 'Income') {
        groups[key].income += t.amount;
      } else {
        groups[key].expense += t.amount;
      }
      // Saldo do mês (Receita - Despesa)
      groups[key].balance = groups[key].income - groups[key].expense;
    });

    // 2. Ordenar cronologicamente (Jan -> Fev -> Mar)
    const sortedKeys = Object.keys(groups).sort();

    // 3. Preparar arrays para o Chart.js
    const labels = sortedKeys.map(key => {
      const [year, month] = key.split('-');
      const date = new Date(year, month - 1);
      // Formata para "nov/25"
      return date.toLocaleDateString('pt-BR', { month: 'short', year: '2-digit' });
    });

    const dataIncome = sortedKeys.map(k => groups[k].income);
    const dataExpense = sortedKeys.map(k => groups[k].expense);
    const dataBalance = sortedKeys.map(k => groups[k].balance);

    return {
      labels,
      datasets: [
        {
          type: 'line',
          label: 'Saldo do Mês',
          borderColor: '#1890ff',
          borderWidth: 2,
          fill: false,
          data: dataBalance,
          tension: 0.3, // Deixa a linha curvinha
          pointBackgroundColor: '#fff',
          pointBorderColor: '#1890ff',
          pointRadius: 4
        },
        {
          type: 'bar',
          label: 'Receitas',
          backgroundColor: 'rgba(63, 134, 0, 0.6)', // Verde
          data: dataIncome,
          borderRadius: 4,
        },
        {
          type: 'bar',
          label: 'Despesas',
          backgroundColor: 'rgba(207, 19, 34, 0.6)', // Vermelho
          data: dataExpense,
          borderRadius: 4,
        },
      ],
    };
  }, [transactions]);

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: {
      mode: 'index',
      intersect: false,
    },
    plugins: {
      legend: { position: 'top' },
      tooltip: {
        callbacks: {
          label: function(context) {
            let label = context.dataset.label || '';
            if (label) {
              label += ': ';
            }
            if (context.parsed.y !== null) {
              label += new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(context.parsed.y);
            }
            return label;
          }
        }
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        grid: { color: '#f0f0f0' }
      },
      x: {
        grid: { display: false }
      }
    },
  };

  return (
    <div style={{ height: 350, width: '100%' }}>
      <Chart type='bar' data={chartData} options={options} />
    </div>
  );
}