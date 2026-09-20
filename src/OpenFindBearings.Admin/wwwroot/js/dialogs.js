// Admin 统一弹窗体系（v1.22.0）
// 改动说明：全站原生 confirm/alert/prompt 散落 20+ 处，风格不一且无法定制；
//   同时 Bootstrap modal 与 offcanvas 各有焦点陷阱，抽屉上叠加 BS modal 会抢焦点导致关不掉。
//   因此确认框/输入框/轻提示全部自绘 position:fixed 覆盖层（纯 jQuery 显隐），
//   视觉沿用 BS 卡片语言，任何页面层级（含抽屉之上）行为一致。
// 对外 API：window.showConfirm(opts) -> Promise<boolean>
//           window.showPrompt(opts)  -> Promise<string|null>
//           window.showToast(type, msg)
//           form[data-confirm] 提交前自动弹确认（data-confirm-danger 红色危险按钮，
//           data-confirm-title 可选标题），确认后以 data-ofb-confirmed 标记放行一次。
(function () {
    'use strict';

    // 懒创建确认/输入共用对话框骨架（单例复用）
    function ensureDialog() {
        if (window.__ofbDialog) return window.__ofbDialog;
        const $mask = $(
            '<div class="ofb-dialog-mask" style="display:none;position:fixed;inset:0;z-index:3100;background:rgba(0,0,0,.5)">' +
            '<div style="position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);width:min(420px,92vw);' +
            'background:#fff;border-radius:8px;box-shadow:0 8px 32px rgba(0,0,0,.35);padding:20px">' +
            '<h6 class="mb-2" data-title style="color:#212529"></h6>' +
            '<p class="mb-3" data-msg style="margin-bottom:0;color:#6c757d"></p>' +
            '<div data-inputwrap style="display:none;margin-top:12px"><textarea class="form-control" rows="3"></textarea>' +
            '<div class="small text-danger mt-1" data-err style="display:none">此项为必填</div></div>' +
            '<div class="d-flex justify-content-end gap-2" style="margin-top:16px">' +
            '<button type="button" class="btn btn-sm btn-secondary" data-cancel>取消</button>' +
            '<button type="button" class="btn btn-sm btn-primary" data-ok>确认</button>' +
            '</div></div></div>');
        $('body').append($mask);
        window.__ofbDialog = {
            $mask: $mask,
            $title: $mask.find('[data-title]'),
            $msg: $mask.find('[data-msg]'),
            $inputWrap: $mask.find('[data-inputwrap]'),
            $input: $mask.find('[data-inputwrap] textarea'),
            $err: $mask.find('[data-err]'),
            $ok: $mask.find('[data-ok]'),
            $cancel: $mask.find('[data-cancel]')
        };
        return window.__ofbDialog;
    }

    let dialogResolve = null; // 当前对话框的 Promise resolve（关闭时统一结算）

    /** 打开共用对话框并返回 Promise；onOk 决定确认时回传什么值 */
    function openDialog(opts, onOk) {
        const d = ensureDialog();
        d.$title.text(opts.title || '操作确认');
        d.$msg.text(opts.message || '').toggle(!!opts.message);
        d.$inputWrap.css('display', opts.input ? 'block' : 'none');
        d.$input.val(opts.value || '');
        d.$err.hide();
        d.$ok.removeClass('btn-primary btn-danger').addClass(opts.danger ? 'btn-danger' : 'btn-primary');
        d.$ok.text(opts.okText || '确认');
        d.$cancel.text(opts.cancelText || '取消');
        d.$mask.show();
        setTimeout(function () { (opts.input ? d.$input : d.$ok).trigger('focus'); }, 50);
        return new Promise(function (resolve) { dialogResolve = resolve; window.__ofbOnOk = onOk; });
    }

    /** 关闭对话框并结算 Promise */
    function closeDialog(ok) {
        const d = ensureDialog();
        d.$mask.hide();
        const resolve = dialogResolve, onOk = window.__ofbOnOk;
        dialogResolve = null; window.__ofbOnOk = null;
        if (resolve) resolve(onOk(ok === true));
    }

    $(function () {
        const d = ensureDialog();
        d.$ok.on('click', function () {
            if (d.$inputWrap.is(':visible')) {
                const v = d.$input.val().trim();
                if (d.$inputWrap.data('required') && !v) { d.$err.show(); return; }
            }
            closeDialog(true);
        });
        d.$cancel.on('click', function () { closeDialog(false); });
        // 点遮罩空白处视为取消
        d.$mask.on('click', function (e) { if (e.target === this) closeDialog(false); });
        // ESC 关闭（对话框可见时拦截，防止连带关闭下层抽屉）
        $(document).on('keydown', function (e) {
            if (e.key === 'Escape' && d.$mask.is(':visible')) { e.stopImmediatePropagation(); closeDialog(false); }
        });

        // data-confirm 表单委托：拦截提交 → 弹确认 → 确认后打标记重提交一次
        $(document).on('submit', 'form[data-confirm]', function (e) {
            const $f = $(this);
            if ($f.data('ofb-confirmed')) { $f.removeData('ofb-confirmed'); return; }
            e.preventDefault();
            showConfirm({
                title: $f.data('confirm-title') || '操作确认',
                message: $f.data('confirm'),
                danger: $f.attr('data-confirm-danger') !== undefined
            }).then(function (ok) {
                if (ok) { $f.data('ofb-confirmed', true); $f.trigger('submit'); }
            });
        });
    });

    /** 确认框：resolve true/false */
    window.showConfirm = function (opts) {
        return openDialog(opts || {}, function (ok) { return ok === true ? true : false; });
    };

    /** 输入框：resolve 文本（取消为 null）；opts.required 时空值不允许提交 */
    window.showPrompt = function (opts) {
        const d = ensureDialog();
        const p = openDialog(Object.assign({ input: true }, opts || {}),
            function (ok) { return ok === true ? d.$input.val().trim() : null; });
        d.$inputWrap.data('required', !!(opts && opts.required));
        return p;
    };

    /** 轻提示：右上角自动消失（type: success/error/warning/info） */
    window.showToast = function (type, msg) {
        const colorMap = { success: '#198754', error: '#dc3545', danger: '#dc3545', warning: '#fd7e14', info: '#0dcaf0' };
        const $t = $('<div>').text(msg).css({
            position: 'fixed', top: '16px', right: '16px', zIndex: 3200, maxWidth: '360px',
            background: colorMap[type] || colorMap.info, color: '#fff', padding: '8px 16px',
            borderRadius: '6px', boxShadow: '0 4px 16px rgba(0,0,0,.25)', fontSize: '14px'
        }).appendTo('body').hide().fadeIn(150);
        setTimeout(function () { $t.fadeOut(300, function () { $t.remove(); }); }, 3000);
    };
})();
