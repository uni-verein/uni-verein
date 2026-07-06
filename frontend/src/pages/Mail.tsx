import React, { useEffect, useState } from 'react';
import { Box, Paper, Tab, Tabs, Typography, useTheme } from '@mui/material';
import * as signalR from '@microsoft/signalr';
import { HubConnection } from '@microsoft/signalr';
import { api } from '../api';
import {
  Attachment,
  EmailState,
  Filter,
  MemberCategory,
  ProgressData,
  SendPayload,
  SummaryData,
} from '../types';
import { useTranslation } from 'react-i18next';
import EmailEditor from '../components/EmailEditor';
import RecipientList from '../components/RecipientList';
import SendProgress from '../components/SendProgress';
import { useSnackbar } from '../components/SnackbarContext';
import useMediaQuery from '@mui/material/useMediaQuery';
import { NIL as NIL_UUID, UUIDTypes } from 'uuid';

export default function Mail() {
  const [filter, setFilter] = useState<Filter>({
    categoryId: NIL_UUID,
    offset: 0,
    limit: 10,
  });
  const { t } = useTranslation();
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [connectionId, setConnectionId] = useState('');
  const [recipients, setRecipients] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedEmails, setSelectedEmails] = useState<string[]>([]);
  const [sendState, setSendState] = useState<EmailState>(EmailState.IDLE);
  const [progress, setProgress] = useState(0);
  const [processed, setProcessed] = useState(0);
  const [logEntries, setLogEntries] = useState<ProgressData[]>([]);
  const [summary, setSummary] = useState<SummaryData | null>(null);
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [activeTab, setActiveTab] = useState('editor');
  const setSnackbar = useSnackbar();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  const [subject, setSubject] = useState('');
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [editorContent, setEditorContent] = useState('');

  const getAccessToken = (): string => {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }
    return token;
  };

  useEffect(() => {
    const conn: HubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/emailProgress', {
        accessTokenFactory: getAccessToken,
      })
      .withAutomaticReconnect()
      .build();

    conn.on('ProgressUpdate', (data: any) => {
      setProgress(data.progress);
      setProcessed(data.processed);
      setLogEntries((prev) => [...prev, data.lastResult]);
    });

    conn.on('SendComplete', (data: SummaryData) => {
      setSummary(data);
      setSendState(EmailState.DONE);
      setProgress(100);
    });

    conn.onreconnected((newConnectionId?: string) => {
      if (newConnectionId) {
        setConnectionId(newConnectionId);
      }
      setConnection(conn);
    });

    conn.onreconnecting(() => {
      setConnection(null);
      setConnectionId('');
    });

    conn.onclose(() => {
      setConnection(null);
      setConnectionId('');
    });

    conn
      .start()
      .then(() => {
        if (conn.connectionId) {
          setConnectionId(conn.connectionId);
        }
        setConnection(conn);
      })
      .catch((err: Error) => console.error('SignalR error:', err));

    return () => {
      conn.stop();
    };
  }, []);

  useEffect(() => {
    const params = new URLSearchParams({
      categoryId: (filter.categoryId !== NIL_UUID ? filter.categoryId : '').toString(),
      limit: filter.limit.toString(),
      offset: filter.offset.toString(),
    });

    api(`/mail/recipients?${params.toString()}`)
      .then((res) => {
        setRecipients(res.items);
        setTotalCount(res.total);
        if (filter.categoryId !== NIL_UUID)
          setSelectedEmails(res.items.map((r: { email: string; name: string }) => r.email));
      })
      .catch((err) => console.error('Receiver loading failed:', err));

    api('/member-categories')
      .then((res) => {
        setMemberCategories(res.items);
      })
      .catch((err) => console.error('Receiver loading failed:', err));
  }, [filter]);

  const getMemberCategoryName = (id: UUIDTypes | null) => {
    if (id === NIL_UUID) 
      return t('pages.mail.memberCategory.customLabel');

    const category = memberCategories.find((x) => x.id === id);
    if (category === undefined) 
      return t('pages.mail.memberCategory.ALL');

    const translationKey = `pages.mail.memberCategory.${category.category}`;
    return t(translationKey).startsWith(translationKey) ? category.name : t(translationKey);
  };

  const handleSend = async (emailData: SendPayload) => {
    const currentConnectionId = connection?.connectionId ?? connectionId;

    if (!currentConnectionId) {
      setSnackbar({ status: 'error', message: t('pages.mail.signalNotConnected') });
      return;
    }

    if (selectedEmails.length === 0) {
      setSnackbar({ status: 'error', message: t('pages.mail.noRecipient') });
      return;
    }

    setSendState(EmailState.SENDING);
    setProgress(0);
    setProcessed(0);
    setLogEntries([]);
    setSummary(null);
    setActiveTab('progress');
    let categoryId: any = null;
    if (filter.categoryId !== NIL_UUID) categoryId = filter.categoryId;

    try {
      const request = {
        categoryId,
        connectionId: currentConnectionId,
        emailData,
        selectedEmails: filter.categoryId === NIL_UUID ? selectedEmails : [],
      };
      await api(`/mail/send`, {
        method: 'POST',
        body: JSON.stringify(request),
      });
    } catch (err) {
      setSnackbar({ status: 'error', message: t('pages.mail.sendingFailed') });
      setSendState(EmailState.IDLE);
    }
  };

  const handleReset = () => {
    setSendState(EmailState.IDLE);
    setProgress(0);
    setProcessed(0);
    setLogEntries([]);
    setSummary(null);
    setActiveTab('editor');
  };

  const progressLabel = (state: EmailState) => {
    let emailState;
    switch (state) {
      case EmailState.SENDING:
        emailState = t('pages.mail.tabLabel.sending');
        break;
      case EmailState.DONE:
        emailState = t('pages.mail.tabLabel.done');
        break;
      default:
        emailState = t('pages.mail.tabLabel.idle');
        break;
    }
    return emailState;
  };

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.mail.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.mail.description', { category: getMemberCategoryName(filter.categoryId) })}
        </Typography>
      </Box>

      <Paper
        elevation={0}
        sx={{
          border: '1px solid',
          borderColor: 'divider',
          borderRadius: 3,
          overflow: 'hidden',
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', position: 'relative', px: 1 }}>
          <Tabs
            value={activeTab}
            onChange={(_, newValue) => setActiveTab(newValue)}
            sx={{
              borderBottom: '1px solid',
              borderColor: 'divider',
              flexGrow: 1,
              minHeight: 'auto',
              minWidth: isMobile ? '100%' : 'auto',
              '& .MuiTabs-flexContainer': {
                flexWrap: 'wrap',
              },
            }}
          >
            <Tab
              value="editor"
              label={t('pages.mail.tabLabel.editor')}
              disabled={sendState === EmailState.SENDING}
            />
            <Tab
              value="recipients"
              label={`${t('pages.mail.tabLabel.recipient')} (${filter.categoryId === NIL_UUID ? selectedEmails.length : totalCount}/${totalCount})`}
              disabled={sendState === EmailState.SENDING}
            />
            <Tab value="progress" label={progressLabel(sendState)} />
          </Tabs>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              ml: isMobile ? 0 : 2,
              mt: isMobile ? 1 : 0,
              mr: isMobile ? 0 : 2,
              width: isMobile ? '100%' : 'auto',
              justifyContent: isMobile ? 'flex-end' : 'flex-start',
            }}
          >
            <Box
              sx={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                backgroundColor: connection?.connectionId ? 'success.main' : 'error.main',
                mr: 1,
              }}
            />
            <Typography variant="body2">
              {t(`pages.mail.signalR.${connection?.connectionId ? 'connected' : 'disconnected'}`)}
            </Typography>
          </Box>
        </Box>

        <Box>
          {activeTab === 'editor' && (
            <EmailEditor
              onSend={handleSend}
              isSending={sendState === EmailState.SENDING}
              recipientCount={filter.categoryId === NIL_UUID ? selectedEmails.length : totalCount}
              subject={subject}
              onSubjectChange={setSubject}
              attachments={attachments}
              onAttachmentsChange={setAttachments}
              initialContent={editorContent}
              onContentChange={setEditorContent}
            />
          )}
          {activeTab === 'recipients' && (
            <RecipientList
              memberCategories={memberCategories}
              recipients={recipients}
              selectedEmails={selectedEmails}
              onChange={setSelectedEmails}
              filter={filter}
              onFilter={setFilter}
              totalCount={totalCount}
            />
          )}
          {activeTab === 'progress' && (
            <SendProgress
              sendState={sendState}
              progress={progress}
              processed={processed}
              total={filter.categoryId === NIL_UUID ? selectedEmails.length : totalCount}
              logEntries={logEntries}
              summary={summary}
              onReset={handleReset}
            />
          )}
        </Box>
      </Paper>
    </Box>
  );
}
