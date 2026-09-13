-- Перенос public.* → схемы сервисов (12.6).
-- Идемпотентно: ON CONFLICT (Id) DO NOTHING, Guid сохраняются.
-- public.* не трогаем — откат = вернуть маршруты шлюза на монолит.
-- Перед запуском схемы должны существовать (миграции EF при старте сервисов).

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('quoting."Specifications"') IS NULL THEN
        RAISE EXCEPTION 'Нет quoting."Specifications" — сначала запустите Quoting (Migrate при старте).';
    END IF;
    IF to_regclass('commercial."Approvals"') IS NULL THEN
        RAISE EXCEPTION 'Нет commercial."Approvals" — сначала запустите Commercial.';
    END IF;
    IF to_regclass('ordering."Orders"') IS NULL THEN
        RAISE EXCEPTION 'Нет ordering."Orders" — сначала запустите Ordering.';
    END IF;
    IF to_regclass('logistics."GoodsReceipts"') IS NULL THEN
        RAISE EXCEPTION 'Нет logistics."GoodsReceipts" — сначала запустите Logistics.';
    END IF;
    IF to_regclass('notifications."Notifications"') IS NULL THEN
        RAISE EXCEPTION 'Нет notifications."Notifications" — сначала запустите Notifications.';
    END IF;
    IF to_regclass('retail."RetailShops"') IS NULL THEN
        RAISE EXCEPTION 'Нет retail."RetailShops" — сначала запустите Retail.';
    END IF;
END $$;

BEGIN;

-- Quoting: родители → дети
INSERT INTO quoting."Specifications" ("Id", "CreatedAtUtc", "Customer", "Title", "UpdatedAtUtc")
SELECT "Id", "CreatedAtUtc", "Customer", "Title", "UpdatedAtUtc"
FROM public."Specifications"
ON CONFLICT DO NOTHING;

INSERT INTO quoting."SpecificationItems" ("Id", "ProductId", "Quantity", "RawName", "RawSku", "SpecificationId")
SELECT "Id", "ProductId", "Quantity", "RawName", "RawSku", "SpecificationId"
FROM public."SpecificationItems"
ON CONFLICT DO NOTHING;

INSERT INTO quoting."QuoteOverrides" ("Id", "CreatedAtUtc", "SpecificationItemId", "UpdatedAtUtc", "UpdatedBy", "VendorId")
SELECT "Id", "CreatedAtUtc", "SpecificationItemId", "UpdatedAtUtc", "UpdatedBy", "VendorId"
FROM public."QuoteOverrides"
ON CONFLICT DO NOTHING;

-- Commercial
INSERT INTO commercial."Approvals" (
    "Id", "Comment", "CostPrice", "CreatedAtUtc", "CreatedBy", "Customer",
    "DecidedAtUtc", "MarginPercent", "MarkupPercent", "SellPrice",
    "SpecificationId", "Status", "Strategy", "Title", "UpdatedAtUtc")
SELECT
    "Id", "Comment", "CostPrice", "CreatedAtUtc", "CreatedBy", "Customer",
    "DecidedAtUtc", "MarginPercent", "MarkupPercent", "SellPrice",
    "SpecificationId", "Status", "Strategy", "Title", "UpdatedAtUtc"
FROM public."Approvals"
ON CONFLICT DO NOTHING;

INSERT INTO commercial."Invoices" (
    "Id", "ApprovalId", "Contract", "CostPrice", "CreatedAtUtc", "CreatedBy",
    "Customer", "DueDateUtc", "MarkupPercent", "Number", "SellPrice", "Status", "UpdatedAtUtc")
SELECT
    "Id", "ApprovalId", "Contract", "CostPrice", "CreatedAtUtc", "CreatedBy",
    "Customer", "DueDateUtc", "MarkupPercent", "Number", "SellPrice", "Status", "UpdatedAtUtc"
FROM public."Invoices"
ON CONFLICT DO NOTHING;

INSERT INTO commercial."InvoiceLines" (
    "Id", "InvoiceId", "LineTotal", "Manufacturer", "Name", "ProductId",
    "Quantity", "Sku", "UnitCost", "UnitPrice", "VendorName")
SELECT
    "Id", "InvoiceId", "LineTotal", "Manufacturer", "Name", "ProductId",
    "Quantity", "Sku", "UnitCost", "UnitPrice", "VendorName"
FROM public."InvoiceLines"
ON CONFLICT DO NOTHING;

INSERT INTO commercial."InvoiceAttachments" (
    "Id", "ContentType", "CreatedAtUtc", "FileContent", "FileName",
    "InvoiceId", "UpdatedAtUtc", "UploadedBy")
SELECT
    "Id", "ContentType", "CreatedAtUtc", "FileContent", "FileName",
    "InvoiceId", "UpdatedAtUtc", "UploadedBy"
FROM public."InvoiceAttachments"
ON CONFLICT DO NOTHING;

-- Ordering
INSERT INTO ordering."Orders" (
    "Id", "CreatedAtUtc", "CreatedBy", "Customer", "MaxLeadTimeDays",
    "Number", "SpecificationId", "Status", "Strategy", "Title", "TotalCost", "UpdatedAtUtc")
SELECT
    "Id", "CreatedAtUtc", "CreatedBy", "Customer", "MaxLeadTimeDays",
    "Number", "SpecificationId", "Status", "Strategy", "Title", "TotalCost", "UpdatedAtUtc"
FROM public."Orders"
ON CONFLICT DO NOTHING;

INSERT INTO ordering."OrderLines" (
    "Id", "LeadTimeDays", "LineTotal", "Name", "OrderId", "ProductId",
    "Quantity", "Sku", "UnitPrice", "VendorId", "VendorName")
SELECT
    "Id", "LeadTimeDays", "LineTotal", "Name", "OrderId", "ProductId",
    "Quantity", "Sku", "UnitPrice", "VendorId", "VendorName"
FROM public."OrderLines"
ON CONFLICT DO NOTHING;

-- Logistics (Outbox не копируем — иначе повторно улетит GoodsReceiptCompleted)
INSERT INTO logistics."GoodsReceipts" (
    "Id", "AccountingRef", "CompletedAtUtc", "CreatedAtUtc", "CreatedBy",
    "Customer", "OrderId", "OrderNumber", "Status", "UpdatedAtUtc", "WarehouseRef")
SELECT
    "Id", "AccountingRef", "CompletedAtUtc", "CreatedAtUtc", "CreatedBy",
    "Customer", "OrderId", "OrderNumber", "Status", "UpdatedAtUtc", "WarehouseRef"
FROM public."GoodsReceipts"
ON CONFLICT DO NOTHING;

INSERT INTO logistics."GoodsReceiptLines" (
    "Id", "GoodsReceiptId", "Name", "OrderedQty", "ProductId", "ReceivedQty", "Sku", "UnitPrice")
SELECT
    "Id", "GoodsReceiptId", "Name", "OrderedQty", "ProductId", "ReceivedQty", "Sku", "UnitPrice"
FROM public."GoodsReceiptLines"
ON CONFLICT DO NOTHING;

-- Notifications
INSERT INTO notifications."Notifications" (
    "Id", "RecipientUserName", "RecipientRole", "Type", "Title", "Message",
    "RelatedEntityType", "RelatedEntityId", "DedupeKey",
    "CreatedAtUtc", "ReadAtUtc", "EmailedAtUtc")
SELECT
    "Id", "RecipientUserName", "RecipientRole", "Type", "Title", "Message",
    "RelatedEntityType", "RelatedEntityId", "DedupeKey",
    "CreatedAtUtc", "ReadAtUtc", "EmailedAtUtc"
FROM public."Notifications"
ON CONFLICT DO NOTHING;

-- Retail: только витрины. Номенклатура/цены живут в catalog, сюда не копируем.
INSERT INTO retail."RetailShops" (
    "Id", "Host", "DisplayName", "Kind", "SearchUrlTemplate",
    "FirstSeenUtc", "LastSuccessUtc", "LastError", "HitCount")
SELECT
    "Id", "Host", "DisplayName", "Kind", "SearchUrlTemplate",
    "FirstSeenUtc", "LastSuccessUtc", "LastError", "HitCount"
FROM public."RetailShops"
ON CONFLICT DO NOTHING;

COMMIT;

-- Счётчики: источник vs приёмник (должно совпасть, если приёмник был пуст или уже копировали те же Id)
SELECT * FROM (
    SELECT 1 AS ord, 'quoting' AS schema, 'Specifications' AS table,
        (SELECT count(*) FROM public."Specifications") AS public_n,
        (SELECT count(*) FROM quoting."Specifications") AS dest_n
    UNION ALL
    SELECT 2, 'quoting', 'SpecificationItems',
        (SELECT count(*) FROM public."SpecificationItems"),
        (SELECT count(*) FROM quoting."SpecificationItems")
    UNION ALL
    SELECT 3, 'quoting', 'QuoteOverrides',
        (SELECT count(*) FROM public."QuoteOverrides"),
        (SELECT count(*) FROM quoting."QuoteOverrides")
    UNION ALL
    SELECT 4, 'commercial', 'Approvals',
        (SELECT count(*) FROM public."Approvals"),
        (SELECT count(*) FROM commercial."Approvals")
    UNION ALL
    SELECT 5, 'commercial', 'Invoices',
        (SELECT count(*) FROM public."Invoices"),
        (SELECT count(*) FROM commercial."Invoices")
    UNION ALL
    SELECT 6, 'commercial', 'InvoiceLines',
        (SELECT count(*) FROM public."InvoiceLines"),
        (SELECT count(*) FROM commercial."InvoiceLines")
    UNION ALL
    SELECT 7, 'commercial', 'InvoiceAttachments',
        (SELECT count(*) FROM public."InvoiceAttachments"),
        (SELECT count(*) FROM commercial."InvoiceAttachments")
    UNION ALL
    SELECT 8, 'ordering', 'Orders',
        (SELECT count(*) FROM public."Orders"),
        (SELECT count(*) FROM ordering."Orders")
    UNION ALL
    SELECT 9, 'ordering', 'OrderLines',
        (SELECT count(*) FROM public."OrderLines"),
        (SELECT count(*) FROM ordering."OrderLines")
    UNION ALL
    SELECT 10, 'logistics', 'GoodsReceipts',
        (SELECT count(*) FROM public."GoodsReceipts"),
        (SELECT count(*) FROM logistics."GoodsReceipts")
    UNION ALL
    SELECT 11, 'logistics', 'GoodsReceiptLines',
        (SELECT count(*) FROM public."GoodsReceiptLines"),
        (SELECT count(*) FROM logistics."GoodsReceiptLines")
    UNION ALL
    SELECT 12, 'notifications', 'Notifications',
        (SELECT count(*) FROM public."Notifications"),
        (SELECT count(*) FROM notifications."Notifications")
    UNION ALL
    SELECT 13, 'retail', 'RetailShops',
        (SELECT count(*) FROM public."RetailShops"),
        (SELECT count(*) FROM retail."RetailShops")
) c
ORDER BY ord;
